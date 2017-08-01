using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using WopiHost.Discovery;
using WopiHost.Discovery.Enumerations;
using WopiHost.Url;

namespace WopiHost.Web.Controllers
{
    public class HomeController : Controller
    {
        public WopiUrlBuilder _urlGenerator;

        private IConfiguration Configuration { get; }

        public string WopiHostUrl => Configuration.GetValue("WopiHostUrl", string.Empty);

        /// <summary>
        /// URL to OWA or OOS
        /// </summary>
        public string WopiClientUrl => Configuration.GetValue("WopiClientUrl", string.Empty);

        public WopiDiscoverer Discoverer => new WopiDiscoverer(new HttpDiscoveryFileProvider(WopiClientUrl));

        //TODO: remove test culture value and load it from configuration SECTION
        public WopiUrlBuilder UrlGenerator => _urlGenerator ?? (_urlGenerator = new WopiUrlBuilder(Discoverer, new WopiUrlSettings { UI_LLCC = new CultureInfo("en-US") }));

        public HomeController(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public async Task<ActionResult> Index([FromQuery]string containerUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(containerUrl))
                {
                    //TODO: root folder id http://wopi.readthedocs.io/projects/wopirest/en/latest/ecosystem/GetRootContainer.html?highlight=EnumerateChildren (use ecosystem controller)
                    string containerId = Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(".\\")));
                    string rootContainerUrl = $"{WopiHostUrl}/wopi/containers/{containerId}";
                    containerUrl = rootContainerUrl;
                }

                var token = (await GetAccessToken(containerUrl)).AccessToken;

                dynamic data = await GetDataAsync(containerUrl + $"/children?access_token={token}");
                foreach (var file in data.ChildFiles)
                {
                    string fileUrl = file.Url.ToString();
                    string fileId = fileUrl.Substring($"{WopiHostUrl}/wopi/files/".Length);
                    fileId = fileId.Substring(0, fileId.IndexOf("?", StringComparison.Ordinal));
                    fileId = Uri.UnescapeDataString(fileId);
                    file.Id = fileId;

                    var fileDetails = await GetDataAsync(fileUrl);
                    file.EditUrl = await UrlGenerator.GetFileUrlAsync(fileDetails.FileExtension.ToString().TrimStart('.'), fileUrl, WopiActionEnum.Edit) + "&access_token=xyz";
                }
                //http://dotnet-stuff.com/tutorials/aspnet-mvc/how-to-render-different-layout-in-asp-net-mvc
                foreach (var container in data.ChildContainers)
                {
                    //TODO create hierarchy
                }

                return View(data);
            }
            catch (DiscoveryException ex)
            {
                return View("Error", ex);
            }
            catch (HttpRequestException ex)
            {
                return View("Error", ex);
            }
        }

        public async Task<ActionResult> Detail(string id)
        {
            string url = $"{WopiHostUrl}/wopi/files/{id}";
            var tokenInfo = await GetAccessToken(url);

            ViewData["access_token"] = tokenInfo.AccessToken;
            //TODO: fix
            //ViewData["access_token_ttl"] = tokenInfo.AccessTokenExpiry;

            dynamic fileDetails = await GetDataAsync(url + $"?access_token={tokenInfo.AccessToken}");
            var extension = fileDetails.FileExtension.ToString().TrimStart('.');
            ViewData["urlsrc"] = await UrlGenerator.GetFileUrlAsync(extension, url, WopiActionEnum.Edit);
            ViewData["favicon"] = await Discoverer.GetApplicationFavIconAsync(extension);
            return View();
        }

        private async Task<dynamic> GetAccessToken(string resourceUrl)
        {
            var getAccessTokenUrl = $"{WopiHostUrl}/wopibootstrapper";
            dynamic accessTokenData = await RequestDataAsync(getAccessTokenUrl, HttpMethod.Post, new Dictionary<string, string> { { "X-WOPI-EcosystemOperation", "GET_NEW_ACCESS_TOKEN" }, { "X-WOPI-WopiSrc", resourceUrl } });
            return accessTokenData.AccessTokenInfo;
        }

        private async Task<dynamic> GetDataAsync(string url)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    using (Stream stream = await client.GetStreamAsync(url))
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            using (var jsonTextReader = new JsonTextReader(sr))
                            {
                                var serializer = new JsonSerializer();
                                return serializer.Deserialize(jsonTextReader);
                            }
                        }
                    }
                }
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException($"It was not possible to read data from '{url}'. Please check the availability of the server.", e);
            }
        }
        private async Task<dynamic> RequestDataAsync(string url, HttpMethod method = null, Dictionary<string, string> headers = null)
        {
            try
            {
                method = method ?? HttpMethod.Get;
                using (HttpClient client = new HttpClient())
                {
                    HttpRequestMessage requestMessage = new HttpRequestMessage(method, url);
                    if (headers != null)
                    {
                        foreach (var header in headers)
                        {
                            requestMessage.Headers.Add(header.Key, header.Value);
                        }
                    }

                    using (HttpResponseMessage responseMessage = await client.SendAsync(requestMessage))
                    {
                        string content = await responseMessage.Content.ReadAsStringAsync();
                        return JsonConvert.DeserializeObject(content);
                    }
                }
            }
            catch (HttpRequestException e)
            {
                throw new HttpRequestException($"It was not possible to read data from '{url}'. Please check the availability of the server.", e);
            }
        }
    }
}
