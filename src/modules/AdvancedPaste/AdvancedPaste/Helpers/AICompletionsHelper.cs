// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Windows.Security.Credentials;
using Newtonsoft.Json;

namespace AdvancedPaste.Helpers
{
    public class AICompletionsHelper
    {
        // Return Response and Status code from the request.
        public struct AICompletionsResponse
        {
            public AICompletionsResponse(string response, int apiRequestStatus)
            {
                Response = response;
                ApiRequestStatus = apiRequestStatus;
            }

            public string Response { get; }

            public int ApiRequestStatus { get; }
        }

        private string _dashScopeKey;

        private string _modelName = "qwen-plus-1127";

        public bool IsAIEnabled => !string.IsNullOrEmpty(this._dashScopeKey);

        public AICompletionsHelper()
        {
            this._dashScopeKey = LoadDashScopeKey();
        }

        public void SetDashScopeKey(string dashScopeKey)
        {
            this._dashScopeKey = dashScopeKey;
        }

        public string GetKey()
        {
            return _dashScopeKey;
        }

        public static string LoadDashScopeKey()
        {
            PasswordVault vault = new PasswordVault();

            try
            {
                PasswordCredential cred = vault.Retrieve("https://dashscope.aliyuncs.com/api-keys", "PowerToys_AdvancedPaste_DashScopeKey");
                if (cred is not null)
                {
                    return cred.Password.ToString();
                }
            }
            catch (Exception)
            {
            }

            return string.Empty;
        }

        private async Task<string> GetAICompletion(string systemInstructions, string userMessage)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _dashScopeKey);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var requestBody = new
                {
                    model = _modelName,
                    messages = new[]
                    {
                        new { role = "system", content = systemInstructions },
                        new { role = "user", content = userMessage }
                    }
                };

                string jsonRequest = JsonConvert.SerializeObject(requestBody);
                HttpContent content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync("https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions", content);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    dynamic responseData = JsonConvert.DeserializeObject(jsonResponse);
                    return responseData.choices[0].message.content;
                }
                else
                {
                    return $"Error: {response.StatusCode} - {response.ReasonPhrase}";
                }
            }
        }

        public async Task<AICompletionsResponse> AIFormatString(string inputInstructions, string inputString)
        {
            string systemInstructions = @"You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to edit their clipboard content as they have requested it.

Do not output anything else besides the reformatted clipboard content.";

            string userMessage = $@"User instructions:
{inputInstructions}

Clipboard Content:
{inputString}

Output:
";

            string aiResponse = null;
            int apiRequestStatus = (int)HttpStatusCode.OK;
            try
            {
                aiResponse = await this.GetAICompletion(systemInstructions, userMessage);

                // Assuming no token usage info is returned by Qwen API, you might need to implement logging based on actual response structure.
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomFormatEvent(0, 0, _modelName));
            }
            catch (Exception error)
            {
                Logger.LogError("GetAICompletion failed", error);
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomErrorEvent(error.Message));
                apiRequestStatus = -1;
            }

            return new AICompletionsResponse(aiResponse, apiRequestStatus);
        }
    }
}
