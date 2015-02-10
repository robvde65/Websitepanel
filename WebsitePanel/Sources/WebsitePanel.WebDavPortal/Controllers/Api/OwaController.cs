﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;
using Cobalt;
using WebsitePanel.EnterpriseServer.Base.HostedSolution;
using WebsitePanel.WebDav.Core;
using WebsitePanel.WebDav.Core.Client;
using WebsitePanel.WebDav.Core.Entities.Owa;
using WebsitePanel.WebDav.Core.Interfaces.Managers;
using WebsitePanel.WebDav.Core.Interfaces.Owa;
using WebsitePanel.WebDav.Core.Interfaces.Security;
using WebsitePanel.WebDav.Core.Security.Cryptography;
using WebsitePanel.WebDav.Core.Wsp.Framework;
using WebsitePanel.WebDavPortal.Configurations.ControllerConfigurations;
using WebsitePanel.WebDavPortal.Extensions;
using WebsitePanel.WebDavPortal.UI.Routes;
using WebsitePanel.WebDav.Core.Extensions;

namespace WebsitePanel.WebDavPortal.Controllers.Api
{
    [Authorize]
    [OwaControllerConfiguration]
    public class OwaController : ApiController
    {
        private readonly IWopiServer _wopiServer;
        private readonly IWebDavManager _webDavManager;
        private readonly IAuthenticationService _authenticationService;
        private readonly IAccessTokenManager _tokenManager;
        private readonly ICryptography _cryptography;
        private readonly ICobaltManager _cobaltManager;

        public OwaController(IWopiServer wopiServer, IWebDavManager webDavManager, IAuthenticationService authenticationService, IAccessTokenManager tokenManager, ICryptography cryptography, ICobaltManager cobaltManager)
        {
            _wopiServer = wopiServer;
            _webDavManager = webDavManager;
            _authenticationService = authenticationService;
            _tokenManager = tokenManager;
            _cryptography = cryptography;
            _cobaltManager = cobaltManager;
        }

        [HttpGet]
        public CheckFileInfo CheckFileInfo(int accessTokenId)
        {
            var token = _tokenManager.GetToken(accessTokenId);

            var fileInfo = _wopiServer.GetCheckFileInfo(token.FilePath);

            var urlPart = Url.Route(FileSystemRouteNames.ShowContentPath, new { org = WspContext.User.OrganizationId, pathPart = token.FilePath });
            var url = new Uri(Request.RequestUri, urlPart).ToString();

            fileInfo.DownloadUrl = url;
            fileInfo.ClientUrl = _webDavManager.GetFileUrl(token.FilePath);

            return fileInfo;
        }

        public HttpResponseMessage GetFile(int accessTokenId)
        {
            var token = _tokenManager.GetToken(accessTokenId);

            var bytes = _webDavManager.GetFileBytes(token.FilePath);

            var result = new HttpResponseMessage(HttpStatusCode.OK);

            var stream = new MemoryStream(bytes);

            result.Content = new StreamContent(stream);
            result.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            return result;
        }

        [HttpPost]
        public HttpResponseMessage Cobalt(int accessTokenId)
        {
            var responseBatch = _cobaltManager.ProcessRequest(accessTokenId, HttpContext.Current.Request.InputStream);

            var correlationId = Request.Headers.GetValues("X-WOPI-CorrelationID").FirstOrDefault() ?? "";

            var response = new HttpResponseMessage();

            response.Content = new PushStreamContent(
                (stream, content, context) =>
                {
                    responseBatch.CopyTo(stream);
                    stream.Close();
                }, "application/octet-stream");

            response.Content.Headers.ContentLength = responseBatch.Length;
            response.Headers.Add("X-WOPI-CorellationID", correlationId);
            response.Headers.Add("request-id", correlationId);

            return response;
        }

        [HttpPost]
        public HttpResponseMessage Lock(int accessTokenId)
        {
           // var token = _tokenManager.GetToken(accessTokenId);

            //_webDavManager.LockFile(token.FilePath);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        [HttpPost]
        public HttpResponseMessage Refresh_Lock(int accessTokenId)
        {
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        [HttpPost]
        public HttpResponseMessage UnLock(int accessTokenId)
        {
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        [HttpPost]
        public async Task<HttpResponseMessage> Put(int accessTokenId)
        {
            var token = _tokenManager.GetToken(accessTokenId);

            var bytes = await Request.Content.ReadAsByteArrayAsync();

            _webDavManager.UploadFile(token.FilePath, bytes);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        [HttpPost]
        public PutRelativeFile Put_Relative(int accessTokenId)
        {
            var result = new PutRelativeFile();

            var token = _tokenManager.GetToken(accessTokenId);

            var newFilePath = string.Empty;

            var target = Request.Headers.Contains("X-WOPI-RelativeTarget") ? Request.Headers.GetValues("X-WOPI-RelativeTarget").First() : Request.Headers.GetValues("X-WOPI-SuggestedTarget").First();

            bool overwrite = Request.Headers.Contains("X-WOPI-RelativeTarget") && Convert.ToBoolean(Request.Headers.GetValues("X-WOPI-OverwriteRelativeTarget").First());

            if (string.IsNullOrEmpty(target))
            {
                throw new HttpResponseException(HttpStatusCode.BadRequest);
            }

            if (target.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries).Count() > 1)
            {
                var fileName = Path.GetFileName(token.FilePath);

                newFilePath = token.FilePath.ReplaceLast(fileName, target);
            }
            else
            {
                newFilePath = Path.ChangeExtension(token.FilePath, target);
            }

            if (overwrite == false && _webDavManager.FileExist(newFilePath))
            {
                throw new HttpResponseException(HttpStatusCode.Conflict);
            }

            var bytes = Request.Content.ReadAsByteArrayAsync().Result;

            _webDavManager.UploadFile(newFilePath, bytes);

            var newToken = _tokenManager.CreateToken(WspContext.User,newFilePath);

            var readUrlPart = Url.Route(FileSystemRouteNames.ViewOfficeOnline, new { org = WspContext.User.OrganizationId, pathPart = newFilePath});
            var writeUrlPart = Url.Route(FileSystemRouteNames.EditOfficeOnline, new { org = WspContext.User.OrganizationId, pathPart = newFilePath });

            result.HostEditUrl = new Uri(Request.RequestUri, writeUrlPart).ToString();
            result.HostViewUrl = new Uri(Request.RequestUri, readUrlPart).ToString(); ;
            result.Name = Path.GetFileName(newFilePath);
            result.Url = Url.GenerateWopiUrl(newToken, newFilePath);

            return result;
        }
    }
}