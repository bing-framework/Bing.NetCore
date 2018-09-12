using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Bing.Extensions.Swashbuckle.Filters.Operations;
using Bing.Logs.Exceptionless;
using Bing.Logs.NLog;
using Bing.Logs.Serilog;
using Bing.Samples.Api.OAuths;
using Bing.Webs.Extensions;
using Bing.Webs.Filters;
using IdentityModel;
using IdentityServer4;
using IdentityServer4.AccessTokenValidation;
using IdentityServer4.Models;
using IdentityServer4.Test;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Swagger;

namespace Bing.Samples.Api
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddMvc(options =>
                {
                    options.Filters.Add<ValidationAttribute>();
                    options.Filters.Add<ResultHandlerAttribute>();
                    options.Filters.Add<ExceptionHandlerAttribute>();
                })
                .AddControllersAsServices();

            services.AddNLog();
            //services.AddExceptionless(options =>
            //{
            //    options.ApiKey = "YDTOG4uvUuEd5BY7uQozsUjaZcPyGz99OE6jNLmp";
            //    options.ServerUrl = "";
            //});
            //services.AddSerilog();
            services.AddSwaggerGen(config =>
            {
                config.SwaggerDoc("v1", new Info() {Title = "Bing.Samples.Api", Version = "v1"});
                var basePath = PlatformServices.Default.Application.ApplicationBasePath;
                var xmlPath = Path.Combine(basePath, "Bing.Samples.Api.xml");
                config.IncludeXmlComments(xmlPath);

                config.OperationFilter<AddRequestHeaderOperationFilter>();
                config.OperationFilter<AddResponseHeadersOperationFilter>();
                config.OperationFilter<AddFileParameterOperationFilter>();

                // 授权组合
                config.OperationFilter<AddSecurityRequirementsOperationFilter>();
                config.OperationFilter<AddAppendAuthorizeToSummaryOperationFilter>();
                config.AddSecurityDefinition("oauth2", new ApiKeyScheme()
                {
                    Description = "Token令牌",
                    In = "header",
                    Name = "Authorization",
                    Type = "apiKey",
                });
            });

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = IdentityServerAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                //.AddIdentityServerAuthentication(options =>
                //{
                //    options.RequireHttpsMetadata = false;
                //    options.ApiName = "Admin"; // API作用域范围
                //    options.Authority = "http://localhost:37680"; // IdentityServer 授权地址
                //})
                .AddJwtBearer(options =>
                {
                    options.Authority = "http://localhost:37680";
                    options.RequireHttpsMetadata = false;
                    options.TokenValidationParameters=new TokenValidationParameters()
                    {
                        ValidateIssuer = true,
                        ValidIssuer = "http://localhost:37680",
                        ValidateAudience = false,
                        ValidAudience = "Admin",
                        ValidateLifetime = true
                    };
                });

            //services.AddAuthorization(options =>
            //{
            //    options.AddPolicy("Admin", authBuilder =>
            //    {
            //        authBuilder.AddAuthenticationSchemes("Bearer");
            //        authBuilder.RequireRole("Admin");
            //    });
            //    options.AddPolicy("Customer", authBuilder =>
            //    {
            //        authBuilder.AddAuthenticationSchemes("Bearer");
            //        authBuilder.RequireRole("Customer");
            //    });
            //});

            //services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");            
            ConfigureIdentityServer4(services);

            return services.AddBing();
        }

        /// <summary>
        /// 配置 IdentityServer4 服务
        /// </summary>
        /// <param name="services">服务集合</param>
        public void ConfigureIdentityServer4(IServiceCollection services)
        {
            // AddIdentityServer：方法在依赖注入系统中注册IdentityServer，它还会注册一个基于内存存储的运行时状态，这对于开发场景非常有用，对于生产环境，您需要一个持久化或共享存储。https://identityserver4.readthedocs.io/en/release/quickstarts/8_entity_framework.html#refentityframeworkquickstart
            // AddDeveloperSigningCredential：在每次启动时，为令牌签名创建一个临时密钥。在生产环境需要一个持久化的密钥。https://identityserver4.readthedocs.io/en/release/topics/crypto.html#refcrypto
            // AddInMemoryApiResources：使用内存存储API资源。
            // AddInMemoryClients：使用内存存储密钥以及客户端
            // AddTestUsers：添加测试用户，用于自定义用户登录
            services.AddIdentityServer()
                .AddDeveloperSigningCredential()
                .AddInMemoryApiResources(GetApiResources())
                .AddInMemoryClients(GetClients())
                .AddTestUsers(GetUsers())
                .AddProfileService<CustomProfileService>()
                .AddResourceOwnerValidator<CustomResourceOwnerPasswordValidator>();
        }

        /// <summary>
        /// 获取Api资源列表，作用域列表
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<ApiResource> GetApiResources()
        {
            return new List<ApiResource>()
            {
                new ApiResource("Admin","管理员",new List<string>(){JwtClaimTypes.Role}),
                new ApiResource("Customer","客户"),
                new ApiResource("EveryOne","匿名")
            };
        }

        /// <summary>
        /// 获取客户端列表，用于限制客户端访问作用域
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<Client> GetClients()
        {
            return new List<Client>()
            {
                // 客户端认证方式
                new Client()
                {
                    ClientId = "bing.admin",
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    // 用于认证的密码
                    ClientSecrets =
                    {
                        new Secret("secret".Sha256())
                    },
                    // 客户端有权访问的作用域范围
                    AllowedScopes = {"Admin","Customer","EveryOne"}
                },
                new Client()
                {
                    ClientId = "bing.customer",
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    ClientSecrets =
                    {
                        new Secret("secret".Sha256())
                    },
                    AllowedScopes = {"Customer","EveryOne"}
                },
                new Client()
                {
                    ClientId = "bing.everyone",
                    AllowedGrantTypes = GrantTypes.ClientCredentials,
                    ClientSecrets =
                    {
                        new Secret("secret".Sha256())
                    },
                    AllowedScopes = {"EveryOne"}
                },
                // 密码认证方式
                new Client()
                {
                    ClientId = "bing.ro.admin",
                    AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
                    AllowOfflineAccess = true,// 启用刷新Token
                    ClientSecrets = new List<Secret>()
                    {
                        new Secret("secret".Sha256())
                    },
                    AllowedScopes = { "Admin", "Customer", "EveryOne"}
                }
            };
        }

        /// <summary>
        /// 获取测试用户
        /// </summary>
        /// <returns></returns>
        public static List<TestUser> GetUsers()
        {
            return new List<TestUser>()
            {
                new TestUser()
                {
                    SubjectId = "1",
                    Username = "admin",
                    Password = "123456",
                    Claims = new List<Claim>()
                    {
                        new Claim(JwtClaimTypes.Role, "admin"),
                        new Claim(JwtClaimTypes.Email, "jianxuanbing@126.com")
                    }
                },
                new TestUser()
                {
                    SubjectId = "2",
                    Username = "test",
                    Password = "123456",
                    Claims = new List<Claim>()
                    {
                        new Claim(JwtClaimTypes.Role, "customer"),
                        new Claim(JwtClaimTypes.Email, "test@126.com")
                    }
                }
            };
        }


        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            CommonConfig(app);

            app.UseSwagger(config => { });
            app.UseSwaggerUI(config =>
            {
                config.IndexStream = () =>
                    GetType().GetTypeInfo().Assembly.GetManifestResourceStream("Bing.Samples.Api.Swagger.index.html");
                config.ShowExtensions();
                config.SwaggerEndpoint("/swagger/v1/swagger.json", "Bing.Samples.Api v1");                
            });
        }

        /// <summary>
        /// 公共配置
        /// </summary>
        /// <param name="app"></param>
        private void CommonConfig(IApplicationBuilder app)
        {
            app.UseErrorLog();
            app.UseStaticHttpContext();
            app.UseRequestLog();
            app.UseIdentityServer();// 启用 IdentityServer4 服务
            app.UseAuthentication();
            ConfigRoute(app);
        }

        /// <summary>
        /// 路由配置，支持区域
        /// </summary>
        /// <param name="app"></param>
        private void ConfigRoute(IApplicationBuilder app)
        {
            app.UseMvc(routes =>
            {
                routes.MapRoute("areaRoute", "{area:exists}/{controller}/{action=Index}/{id?}");
                routes.MapRoute("default", "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
