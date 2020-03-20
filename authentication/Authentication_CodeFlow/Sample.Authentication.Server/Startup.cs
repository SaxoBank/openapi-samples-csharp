using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Sample.Authentication.Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc()
                    .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                    .AddJsonOptions(opt => SetJsonFormat(opt));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseMvc();
        }

        /// <summary>
        /// Define Json Format
        /// </summary>
        /// <param name="option"></param>
        private void SetJsonFormat(MvcJsonOptions option)
        {
            option.SerializerSettings.ContractResolver = new DefaultContractResolver();
            option.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
            option.SerializerSettings.Formatting = Formatting.Indented;
            option.SerializerSettings.MaxDepth = 4;
            option.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Serialize;
            option.SerializerSettings.DateFormatString = "yyyy-MM-ddThh:mm:ss.fff";
        }
    }
}
