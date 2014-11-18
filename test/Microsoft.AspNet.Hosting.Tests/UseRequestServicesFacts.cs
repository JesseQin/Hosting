﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNet.Builder;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Fallback;
using Xunit;
using Microsoft.AspNet.PipelineCore;
using Microsoft.AspNet.RequestContainer;
using System;
using Microsoft.Framework.Logging;
using Microsoft.AspNet.Hosting.Builder;
using Microsoft.AspNet.Hosting.Startup;
using Microsoft.AspNet.Hosting.Server;

namespace Microsoft.AspNet.Hosting.Tests
{
    public class RequestServicesContainerFacts
    {
        [Fact]
        public void RequestServicesAvailableOnlyAfterRequestServices()
        {
            var baseServiceProvider = new ServiceCollection()
                .Add(HostingServices.GetDefaultServices())
                .BuildServiceProvider();
            var builder = new ApplicationBuilder(baseServiceProvider);

            bool foundRequestServicesBefore = false;
            builder.Use(next => async c =>
            {
                foundRequestServicesBefore = c.RequestServices != null;
                await next.Invoke(c);
            });
            builder.UseRequestServices();
            bool foundRequestServicesAfter = false;
            builder.Use(next => async c =>
            {
                foundRequestServicesAfter = c.RequestServices != null;
                await next.Invoke(c);
            });

            var context = new DefaultHttpContext();
            builder.Build().Invoke(context);
            Assert.False(foundRequestServicesBefore);
            Assert.True(foundRequestServicesAfter);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EnsureRequestServicesSetsRequestServices(bool initializeApplicationServices)
        {
            var baseServiceProvider = new ServiceCollection()
                .Add(HostingServices.GetDefaultServices())
                .BuildServiceProvider();
            var builder = new ApplicationBuilder(baseServiceProvider);

            bool foundRequestServicesBefore = false;
            builder.Use(next => async c =>
            {
                foundRequestServicesBefore = c.RequestServices != null;
                await next.Invoke(c);
            });
            builder.Use(next => async c =>
            {
                using (var container = RequestServicesContainer.EnsureRequestServices(c, baseServiceProvider))
                {
                    await next.Invoke(c);
                }
            });
            bool foundRequestServicesAfter = false;
            builder.Use(next => async c =>
            {
                foundRequestServicesAfter = c.RequestServices != null;
                await next.Invoke(c);
            });

            var context = new DefaultHttpContext();
            if (initializeApplicationServices)
            {
                context.ApplicationServices = baseServiceProvider;
            }
            builder.Build().Invoke(context);
            Assert.False(foundRequestServicesBefore);
            Assert.True(foundRequestServicesAfter);
        }

        [Theory]
        [InlineData(typeof(IHostingEngine))]
        [InlineData(typeof(IServerManager))]
        [InlineData(typeof(IStartupManager))]
        [InlineData(typeof(IStartupLoaderProvider))]
        [InlineData(typeof(IApplicationBuilderFactory))]
        [InlineData(typeof(IStartupLoaderProvider))]
        [InlineData(typeof(IHttpContextFactory))]
        [InlineData(typeof(ITypeActivator))]
        [InlineData(typeof(IApplicationLifetime))]
        [InlineData(typeof(ILoggerFactory))]
        public void UseRequestServicesHostingImportedServicesAreDefined(Type service)
        {
            var baseServiceProvider = new ServiceCollection()
                .Add(HostingServices.GetDefaultServices())
                .BuildServiceProvider();
            var builder = new ApplicationBuilder(baseServiceProvider);

            builder.UseRequestServices();

            Assert.NotNull(builder.ApplicationServices.GetRequiredService(service));
        }

    }
}