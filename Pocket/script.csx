using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class Script : ScriptBase
{
    private const string OAuthRequestURL = "https://getpocket.com/v3/oauth/request";
    private const string OAuthAuthorizeURL = "https://getpocket.com/v3/oauth/authorize";
    private const string ContentType = "application/json; charset=UTF-8";
    private const string XAccept = "application/json";

    public override async Task<HttpResponseMessage> ExecuteAsync()
    {
        if (this.Context.OperationId == "RequestTokenPost") 
        {
            //Get consumer key header value
            var consumerKey = this.Context.Request.Headers.GetValues("consumer_key").FirstOrDefault();

            //Retrieve request token
            var requestBodyForRequestToken = new Dictionary<string, string>
            {
                { "consumer_key", consumerKey },
                { "redirect_uri", "https://make.powerautomate.com" }
            };
            var requestToken = await GetToken(OAuthRequestURL, requestBodyForRequestToken, "code");

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonConvert.SerializeObject(new { request_token = requestToken, ACTION_REQUIRED = "You must now copy the following URL and paste it in a browser window: https://getpocket.com/auth/authorize?request_token=" + requestToken + "&redirect_uri=https://make.powerautomate.com" }), Encoding.UTF8, "application/json")
            };
        }

        if (this.Context.OperationId == "AccessTokenPost") 
        {
            //Get consumer key header value
            var consumerKey = this.Context.Request.Headers.GetValues("consumer_key").FirstOrDefault();
            if (string.IsNullOrEmpty(consumerKey))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Missing consumer_key header")
                };
            }

            var requestToken = this.Context.Request.Headers.GetValues("request_token").FirstOrDefault();
            if (string.IsNullOrEmpty(requestToken))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Missing request_token header")
                };
            }

            //Retrieve access token
            var requestBodyForAccessToken = new Dictionary<string, string>
            {
                { "consumer_key", consumerKey },
                { "code", requestToken }
            };
            var accessToken = await GetToken(OAuthAuthorizeURL, requestBodyForAccessToken, "access_token");

            // Check if accessToken is null
            if (string.IsNullOrEmpty(accessToken))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(new { error = "Failed to get access token", consumer_key = consumerKey, request_token = requestToken }), Encoding.UTF8, "application/json")
                };
            }

            //Return access token
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonConvert.SerializeObject(new { access_token = accessToken }), Encoding.UTF8, "application/json")
            };
        }
        else
        {
            // Send the current request and return the response
            return await this.Context.SendAsync(this.Context.Request, this.CancellationToken);
        }
    }

    private async Task<string> GetToken(string url, Dictionary<string, string> requestBody, string tokenKey)
    {
        Uri requestUrl = new Uri(url);
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        var jsonRequestBody = JsonConvert.SerializeObject(requestBody);
        request.Content = new StringContent(jsonRequestBody, Encoding.UTF8, "application/json");
        request.Headers.Clear();
        request.Headers.TryAddWithoutValidation("Content-Type", ContentType);
        request.Headers.TryAddWithoutValidation("X-Accept", XAccept);
        HttpResponseMessage response = await this.Context.SendAsync(request, this.CancellationToken);
        var responseString = await response.Content.ReadAsStringAsync();
        
        // Check if responseString is a JSON object
        if (responseString.Trim().StartsWith("{") && responseString.Trim().EndsWith("}"))
        {
            var jsonResponse = JObject.Parse(responseString);
            jsonResponse.TryGetValue(tokenKey, out JToken token);
            return token?.ToString();
        }
        else
        {
            return null;
        }
    }
}