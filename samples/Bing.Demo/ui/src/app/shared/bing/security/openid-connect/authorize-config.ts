// ============== OpenId Connect授权配置 ==========
// Copyright 2019 简玄冰
// Licensed under the MIT license
// ================================================

/**
 * OpenId Connect 授权配置
 */
export class AuthorizeConfig {
    /**
     * OpenId Connect 认证服务入口地址，该项为必填
     */
    authority: string;
    /**
     * 客户端标识，该项为必填
     */
    clientId: string;
    /**
     * 资源访问范围，默认值：openid profile
     */
    scope = 'openid profile';
    /**
     * 响应类型，默认值：id_token token
     */
    responseType = 'id_token token';
    /**
     * 登录回调页面地址，默认值：/callback，注意:该值必须与OpenId Connect认证服务配置的登录回调地址匹配
     */
    redirectUri = '/callback';
    /**
     * 注销回调页面地址，默认值：/，注意:该值必须与OpenId Connect认证服务配置的注销回调地址匹配
     */
    postLogoutRedirectUri: string;

    /**
     * 验证
     */
    validate() {
        if (!this.authority)
            throw new Error('OpenId Connect认证服务入口地址未设置，请设置AuthorizeConfig的authority属性');
        if (!this.clientId)
            throw new Error('OpenId Connect客户端标识未设置，请设置AuthorizeConfig的clientId属性');
    }

    /**
     * 获取登录回调页面地址
     */
    getRedirectUri() {
        if (this.redirectUri === '/callback')
            return `${location.origin}${this.redirectUri}`;
        return this.redirectUri;
    }

    /**
     * 获取注销回调页面地址
     */
    getPostLogoutRedirectUri() {
        if (!this.postLogoutRedirectUri)
            return location.origin;
        return this.postLogoutRedirectUri;
    }
}
