// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.Hosting.Server;
using Microsoft.AspNet.Http;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Infrastructure;
using Microsoft.Framework.DependencyInjection.ServiceLookup;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.AspNet.TestHost
{
    public class TestServer : IServerFactory, IDisposable
    {
        private const string DefaultEnvironmentName = "Development";
        private const string ServerName = nameof(TestServer);
        private static readonly ServerInformation ServerInfo = new ServerInformation();
        private Func<object, Task> _appDelegate;
        private IDisposable _appInstance;
        private bool _disposed = false;

        public TestServer(IConfiguration config, IServiceProvider serviceProvider, Action<IApplicationBuilder> appStartup)
        {
            var appEnv = serviceProvider.GetRequiredService<IApplicationEnvironment>();

            HostingContext hostContext = new HostingContext()
            {
                ApplicationName = appEnv.ApplicationName,
                Configuration = config,
                ServerFactory = this,
                Services = serviceProvider,
                ApplicationStartup = appStartup
            };

            var engine = serviceProvider.GetRequiredService<IHostingEngine>();
            _appInstance = engine.Start(hostContext);
        }

        public Uri BaseAddress { get; set; } = new Uri("http://localhost/");

        public static TestServer Create(Action<IApplicationBuilder> app)
        {
            var services = new ServiceCollection();
            services.Import(CallContextServiceLocator.Locator.ServiceProvider);
            return Create(services, app: app);
        }

        public static TestServer Create(IServiceCollection services, Action<IApplicationBuilder> app)
        {
            services.Add(HostingServices.GetDefaultServices(false));
            services.AddSingleton<IHostingEnvironment, TestHostingEnvironment>();
            services.AddInstance<IServiceManifest>(new ServiceManifest(services));

            var appServices = services.BuildServiceProvider();
            var config = new Configuration();
            return new TestServer(config, appServices, app);
        }

        public HttpMessageHandler CreateHandler()
        {
            var pathBase = BaseAddress == null ? PathString.Empty : PathString.FromUriComponent(BaseAddress);
            if (pathBase.Equals(new PathString("/")))
            {
                // When we just have http://host/ the trailing slash is really part of the Path, not the PathBase.
                pathBase = PathString.Empty;
            }
            return new ClientHandler(Invoke, pathBase);
        }

        public HttpClient CreateClient()
        {
            return new HttpClient(CreateHandler()) { BaseAddress = BaseAddress };
        }

        /// <summary>
        /// Begins constructing a request message for submission.
        /// </summary>
        /// <param name="path"></param>
        /// <returns><see cref="RequestBuilder"/> to use in constructing additional request details.</returns>
        public RequestBuilder CreateRequest(string path)
        {
            return new RequestBuilder(this, path);
        }

        public IServerInformation Initialize(IConfiguration configuration)
        {
            return ServerInfo;
        }

        public IDisposable Start(IServerInformation serverInformation, Func<object, Task> application)
        {
            if (!(serverInformation.GetType() == typeof(ServerInformation)))
            {
                throw new ArgumentException(string.Format("The server must be {0}", ServerName), "serverInformation");
            }

            _appDelegate = application;

            return this;
        }

        public Task Invoke(object env)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
            return _appDelegate(env);
        }

        public void Dispose()
        {
            _disposed = true;
            _appInstance.Dispose();
        }

        private class ServerInformation : IServerInformation
        {
            public string Name
            {
                get { return TestServer.ServerName; }
            }
        }

        private class TestHostingEnvironment : IHostingEnvironment
        {
            public TestHostingEnvironment(IApplicationEnvironment appEnv)
            {
                WebRoot = HostingUtilities.GetWebRoot(appEnv.ApplicationBasePath);
            }

            public string EnvironmentName { get { return DefaultEnvironmentName; } }

            public string WebRoot { get; private set; }
        }

        private class ServiceManifest : IServiceManifest
        {
            public ServiceManifest(IServiceCollection services)
            {
                // REVIEW: Dedupe services, also drop generics, should we also drop scopes or blowup?  since this is test code
                Services = services.Where(s => s.ServiceType.GenericTypeArguments.Length == 0).Select(s => s.ServiceType).Distinct();
            }

            public IEnumerable<Type> Services { get; private set; }
        }


    }
}
