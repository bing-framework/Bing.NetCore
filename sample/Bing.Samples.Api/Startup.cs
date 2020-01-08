using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using AspectCore.Extensions.DependencyInjection;
using Bing.AspNetCore;
using Bing.AutoMapper;
using Bing.Biz.Payments.Extensions;
using Bing.EasyCaching;
using Bing.Extensions.Swashbuckle.Configs;
using Bing.Extensions.Swashbuckle.Core;
using Bing.Extensions.Swashbuckle.Extensions;
using Bing.Extensions.Swashbuckle.Filters.Operations;
using Bing.Logs.Exceptionless;
using Bing.Logs.Log4Net;
using Bing.Core;
using Bing.Locks.Default;
using Bing.Logs.NLog;
using Bing.Logs.Serilog;
using Bing.Samples.Api.OAuths;
using Bing.Webs.Extensions;
using Bing.Webs.Filters;
using EasyCaching.Core;
using EasyCaching.CSRedis;
using EasyCaching.Serialization.Json;
using IdentityModel;
using IdentityServer4.AccessTokenValidation;
using IdentityServer4.Models;
using IdentityServer4.Test;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
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
            // 添加MVC服务
            services.AddMvc(options =>
                {
                    options.Filters.Add<ValidationModelAttribute>();
                    options.Filters.Add<ResultHandlerAttribute>();
                    options.Filters.Add<ExceptionHandlerAttribute>();
                })
                .AddJsonOptions(options =>
                {
                    options.SerializerSettings.DateFormatString = "yyyy-MM-dd HH:mm:ss";
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddControllersAsServices();

            services.AddPay(x =>
            {
                x.AlipayOptions.AppId = "";
                x.AlipayOptions.GatewayUrl = "https://openapi.alipaydev.com/gateway.do";
                x.AlipayOptions.PrivateKey =
                    "MIIEogIBAAKCAQEAmrTD6XAUY9wM6FCRJMiG1osJ1tiYVa10IazObKfHTBc0foCfcT6MpUVdlyXzGXRbd2D4HL0+NVkLejz5vqpCTHxO0BXD0zBt0r2pCBdXt0PkP21DVz4k9VbX29T0AKTcZ01AAdUd3m4jfqe2jszG1z8j6QcMzH+Nz3aIu2qO7DW+u3WOvIPzpS3CTFCxWYlcgXQnfI5naGd7V8sLV4GhDa3iU3kWklYSBjFNGnOkMcz5hd98mNE5zP35x7fO+ks55eS400I4ZYSiOlHFSAD0KzW3G8K1DSUBpv6FyUqeGW9ckWlgvU6oNec3pC/lkOmirTuBvQMAmCZNjtWfOQIBHQIDAQABAoIBAA4Lp1XERTWjvtBAsEzEn+lOikAlPf9ZVhfQlpUqzl9MJAnwJ4migiZnG84jNeTzuXInLZ9+Vu2E/hPFAW+cCZTkHEusDjFYTkA50+TWKbKLyWcwxlJfY/+aONLOjLCaRyBh1RPVg3a0TSislVh1ov/bzajUaQcP9ZIGUveg/wTW4CreFezetJbOz/CFokc+PazFRJYiq/9rN2Tzhn3hi6V5ICTLGQJmB7/AM35e7SFc0w+lkUMLSX9bMVpgDoFVAgo8LGMysC8xf5z5ahkONR3w551c/bNJjPy2KDUiBg6sVATvU/l4xXzM19ZqMQ9JIiTuf4YdX7Chqx6RbgLbO0ECgYEAy6dQ3kJ2X++X9hZ+jWY3QqLc82F/fLd845QGoau5xn4aTYQwH5jA5Ff3BqzAQMgJOKPMLd6GmH4Y249howTv5MbPfihaCfKB4pjdMfHyceNYLillQtuZaIpyucmTHERLZ8afgzybBxPn7DT6Mk1xfOp6bmN4+h0pw/N2PKIfGVkCgYEAwnijqmmBFHYxFif8L0V5/SvSxzEIdQ5mpH4y7OxnJ0Yka65cP5+HfXVvvrPQUp7yHcwbA1I7olAn8t6GfoHP3y3J0nzS3hVQwvdgIq0KXv5gm93F3ZKObQrbRJYJdkWXffz76E+I6DqX67E1jP5cztjXbdkzCWfpL9id6zmj6WUCgYAe4C8Sg2EPCnQvixmEtoqKP8bf31hEwEze9AJNYIu53ESAnBnvsGkONYfuKyK6r5k2TR8XlTUyyWtbXlGfNZBpTvsGVXfRKkMm56YhfF0VhzJHTV9c045emx7pq/XxwyjrguGMNBQM7qeq2B1WowchuSr2sX4V7XX3j2HNr4angQKBgG8hV43Lirrxq61YnjE5R7PYdjPkHkweNaOshleD5JK575glZIvrExcro/bbdKGyOPO0Ln+gX3mqyplsdnkWn36PAPUq5amJjsRLbwGB1xpfzT9k5WxwErnXaWPxRWjz7dVOW3nu8XKcATLr6oku1kRSABHC+/pVChmQdPX102hNAoGAFUlrtw7N6McE7+ttJUEOW/9rSq7Hjq1j3/9N/5YTVtZnnNlpOKt0eAQ+z7I0vapKjl5hjKAbFyFh6fYy4Z+u5WTg+o4nOaUNOBcD3Mges81pFLb2V+ONWV8QJwE4t0rVt6JEECP6nTRa8ZTRdd9b5UDkrFxxyVRavrJIREFrEow=";
                x.AlipayOptions.PublicKey =
                    "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAn7Oopue3a2x44FgJlWGM3S3QBTz6mBrCRqcvTeX5qH5StlvbF68J34oV3wt+QPn0YIskIgJMC9YwsDKUCPG1k9SjnlKy6hAbHeiAyt2boW9PfoTPYEV36ZH54jzZDGG7k34ZD1EbB3LZnvqpqLwGXQFglg5Xq52eUK6St2wysNzqHlx/WFt6m3OVfKg55udkF1RzBujy1B8Ym8+7YQmD/Ruty+eszBQUOC4nfqq8DsJ3LDMU7AX0J9leuQnReFLq+wCErJSQw/1fplCt3S7iETG7VrCNNRQ5evL8UcaNkDwT0SC+qukhX07Se6Tte61Wur3d6t8IkaeuS1oMQv04qwIDAQAB";
            });
            // 添加NLog日志操作
            //services.AddNLog();

            // 多日志输出
            //services.AddLog4NetWithFactory();
            //services.AddNLogWithFactory();
            //services.AddSerilogWithFactory();
            //services.AddExceptionlessWithFactory(options =>
            //{
            //    options.ApiKey = "YDTOG4uvUuEd5BY7uQozsUjaZcPyGz99OE6jNLmp";
            //    options.ServerUrl = "";
            //});

            //services.AddExceptionless(options =>
            //{
            //    options.ApiKey = "5K9YStkK1AUMz5FrWLtZghEcBEUGPuU1UoRjVp47";
            //    options.ServerUrl = "http://192.168.0.66:65000";
            //});
            //services.AddSerilog();

            services.AddAutoMapper();
            // 添加swagger
            services.AddSwaggerCustom(CurrentSwaggerOptions);

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

            // 添加缓存
            services.AddCaching(options =>
            {
                options.UseCSRedis(config =>
                {
                    config.MaxRdSecond = 0;
                    config.DBConfig = new CSRedisDBOptions()
                    {
                        ConnectionStrings = new List<string>()
                        {
                            "127.0.0.1:6379,defaultDatabase=1,poolsize=10"
                        }
                    };
                }).WithJson();
            });

            // 添加业务锁
            services.AddLock();

            services.AddUploadService();
            services.AddApiInterfaceService();
            services.AddBing<AspNetCoreBingModuleManager>();
            return services.BuildServiceContextProvider();
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
            Bing.Utils.Helpers.Web.Environment = env;
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            CommonConfig(app);
            app.UseSwaggerCustom(CurrentSwaggerOptions);
            app.UseBing();
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
            app.UseEasyCaching();
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

        /// <summary>
        /// 项目接口文档配置
        /// </summary>
        private CustomSwaggerOptions CurrentSwaggerOptions = new CustomSwaggerOptions()
        {
            ProjectName = "Bing.Samples.Api 在线文档调试",
            UseCustomIndex = true,
            RoutePrefix = "swagger",
            ApiVersions = new List<Extensions.Swashbuckle.Configs.ApiVersion>() { new Extensions.Swashbuckle.Configs.ApiVersion() { Version = "v1"} },
            SwaggerAuthorizations = new List<CustomSwaggerAuthorization>()
            {
            },
            AddSwaggerGenAction = config =>
            {
                config.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "Bing.Samples.Api.xml"), true);

                config.OperationFilter<RequestHeaderOperationFilter>();
                config.OperationFilter<ResponseHeadersOperationFilter>();
                config.OperationFilter<FileParameterOperationFilter>();

                // 授权组合
                config.OperationFilter<SecurityRequirementsOperationFilter>();
                config.OperationFilter<AppendAuthorizeToSummaryOperationFilter>();
                config.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>>()
                    {{"oauth2", new string[] { }}});

                config.AddSecurityDefinition("oauth2", new ApiKeyScheme()
                {
                    Description = "Token令牌",
                    In = "header",
                    Name = "Authorization",
                    Type = "apiKey",
                });
                // 设置所有参数为驼峰式命名
                config.DescribeAllParametersInCamelCase();
            },
            UseSwaggerAction = config =>
            {
            },
            UseSwaggerUIAction = config =>
            {
                config.InjectJavascript("/swagger/resources/jquery");
                config.InjectStylesheet("/swagger/resources/swagger-common");
                config.UseDefaultSwaggerUI();
            }
        };
    }
}
