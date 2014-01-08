﻿using Microsoft.Phone.Info;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FHSDK.FHHttpClient
{
    /// <summary>
    /// Base class for all the API requests
    /// </summary>
    public abstract class FHRequest
    {
        const double DEFAULT_TIMEOUT = 30*1000;

        /// <summary>
        /// The app configurations
        /// </summary>
        protected AppProps appProps;
        private TimeSpan timeout = TimeSpan.FromMilliseconds(DEFAULT_TIMEOUT);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="appProps"></param>
        public FHRequest(AppProps appProps)
        {
            this.appProps = appProps;
        }

        /// <summary>
        /// Get or set the timeout value
        /// </summary>
        public TimeSpan TimeOut
        {
            get
            {
                return timeout;
            }

            set
            {
                this.timeout = value;
            }
        }

        /// <summary>
        /// Exxcute the request asynchronously
        /// </summary>
        /// <returns>Server response</returns>
        public async Task<FHResponse> execAsync()
        {
            string uri = GetUri();
            IDictionary<string, object> requestParams = GetRequestParams();
            FHResponse fhres = await FHHttpClient.PostAsync(uri, requestParams, this.timeout);
            return fhres;
        }

        /// <summary>
        /// Get the default request parameters
        /// </summary>
        /// <returns></returns>
        protected IDictionary<string, object> GetDefaultParams()
        {
            Dictionary<string, object> defaults = new Dictionary<string, object>();
            defaults["appid"] = appProps.appid;
            defaults["appkey"] = appProps.appkey;
            defaults["cuid"] = this.UUID;
            defaults["destination"] = "windowsphone";
            defaults["sdk_version"] = "FH_WINDOWNSPHONE_SDK/" + FH.SDK_VERSION;
            if (null != this.appProps.projectid)
            {
                defaults["projectid"] = this.appProps.projectid;
            }
            JObject initInfo = GetInitInfo();
            if (null != initInfo)
            {
                defaults["init"] = initInfo;
            }
            return defaults;
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        protected JObject GetInitInfo()
        {
            IsolatedStorageSettings settings = IsolatedStorageSettings.ApplicationSettings;
            string initValue;
            settings.TryGetValue<string>("init", out initValue);
            JObject initInfo = null;
            if (null != initValue)
            {
                initInfo = JObject.Parse(initValue);
            }
            return initInfo;
        }

        /// <summary>
        /// Get the unique client id for analytics.
        /// </summary>
        /// <remarks>
        /// It will use Windows Phone ANID by default. If it's not avaiable, the unique device id will be used.
        /// </remarks>
        protected string UUID
        {
            get
            {
                string retVal = "";
                object uuid;
                UserExtendedProperties.TryGetValue("ANID2", out uuid);
                if (null != uuid)
                {
                    retVal = uuid.ToString().Substring(2, 32);
                }
                else
                {
                    DeviceExtendedProperties.TryGetValue("DeviceUniqueId", out uuid);
                    if (null != uuid)
                    {
                        retVal = Convert.ToBase64String((byte[])uuid);
                    }
                }
                return retVal;
            }
        }

        /// <summary>
        /// Construct the remote uri based on the request type
        /// </summary>
        /// <returns></returns>
        public abstract string GetUri();

        /// <summary>
        /// Construct the request data based on the request type
        /// </summary>
        /// <returns></returns>
        public abstract IDictionary<string, object> GetRequestParams();

    }

    /// <summary>
    /// Class represents init requests
    /// </summary>
    public class FHInitRequest : FHRequest
    {
        const string INIT_PATH = "app/init";
        
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="appProps"></param>
        public FHInitRequest(AppProps appProps)
            : base(appProps)
        {
        }

        /// <summary>
        /// Construct the remote uri based on the request type
        /// </summary>
        /// <returns></returns>
        public override string GetUri()
        {
            return String.Format("{0}/{1}", this.appProps.host, INIT_PATH);
        }

        /// <summary>
        /// Construct the request data based on the request type
        /// </summary>
        /// <returns></returns>
        public override IDictionary<string, object> GetRequestParams()
        {
            return GetDefaultParams();
        }
    }

    /// <summary>
    /// Class represents act requests
    /// </summary>
    public class FHActRequest : FHRequest
    {
        private JObject cloudProps;

        /// <summary>
        /// Get or set the remote cloud function name
        /// </summary>
        public string RemoteAct { get; set; }

        /// <summary>
        /// Get or set the request parameters
        /// </summary>
        public IDictionary<string, object> RequestParams { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="appProps"></param>
        /// <param name="cloudProps"></param>
        public FHActRequest(AppProps appProps, JObject cloudProps)
            : base(appProps)
        {
            this.cloudProps = cloudProps;
        }

        /// <summary>
        /// Execute the act request asynchronously
        /// </summary>
        /// <param name="remoteAct">The name of the cloud action</param>
        /// <param name="requestParams">The request parameters</param>
        /// <returns></returns>
        public async Task<FHResponse> execAsync(string remoteAct, IDictionary<string, object> requestParams)
        {
            this.RemoteAct = remoteAct;
            this.RequestParams = requestParams;
            return await this.execAsync();
        }

        /// <summary>
        /// Construct the remote uri based on the request type
        /// </summary>
        /// <returns></returns>
        public override string GetUri()
        {
            if (null == this.RemoteAct)
            {
                throw new InvalidOperationException("RemoteAction can not be null");
            }
            string uri = null;
            string appMode = this.appProps.mode;

            if (null != cloudProps["url"])
            {
                uri = (string) cloudProps["url"];
            }
            else
            {
                JObject hosts = (JObject) cloudProps["hosts"];
                if ("dev" == appMode)
                {
                    uri = (string) hosts["debugCloudUrl"];
                }
                else
                {
                    uri = (string) hosts["releaseCloudUrl"];
                }
            }
            uri = uri.EndsWith("/") ? uri.Substring(0, uri.Length - 1) : uri;
            return String.Format("{0}/{1}/{2}", uri, "cloud", this.RemoteAct);
        }

        /// <summary>
        /// Construct the request data based on the request type
        /// </summary>
        /// <returns></returns>
        public override IDictionary<string, object> GetRequestParams()
        {
            Dictionary<string, object> data = new Dictionary<string, object>();
            if(null != this.RequestParams)
            {
                data = new Dictionary<string, object>(this.RequestParams);
            }
            IDictionary<string, object> defaultParams = GetDefaultParams();
            data["__fh"] = defaultParams;
            return data;
        }
    }

}
