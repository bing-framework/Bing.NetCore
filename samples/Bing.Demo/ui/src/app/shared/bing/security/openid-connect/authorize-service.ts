// ============== OpenId Connect授权服务 ==========
// Copyright 2019 简玄冰
// Licensed under the MIT license
// ================================================
import { Injectable } from '@angular/core';
import { User, UserManager } from 'oidc-client';
import { bing } from '../../index';
import { AuthorizeConfig } from './authorize-config';

/**
 * OpenId Connect授权服务
 */
@Injectable()
export class AuthorizeService {
    /**
     * 用户管理
     */
    private manager: UserManager;

    /**
     * 初始化OpenId Connect授权服务
     * @param config 授权配置
     */
    constructor(private config: AuthorizeConfig) {
        this.manager = new UserManager(this.getConfig());
    }

    /**
     * 获取配置
     */
    private getConfig() {
        this.config.validate();
        return {
            authority: this.config.authority,
            client_id: this.config.clientId,
            response_type: this.config.responseType,
            scope: this.config.scope,
            redirect_uri: this.config.getRedirectUri(),
            post_logout_redirect_uri: this.config.getPostLogoutRedirectUri()
        };
    }

    /**
     * 是否认证成功
     * @param user 用户
     */
    isAuthenticated(user: User) {
        return user && user.profile && user.profile.sub;
    }

    /**
     * 登陆，重定向到认证服务进行登录请求
     */
    login() {
        this.manager.signoutRedirect();
    }

    /**
     * 登录回调
     * @param options OpenId Connect认证登陆回调配置
     */
    async loginCallback(options: IOidcLoginCallbackOptions = null) {
        options = options || {};
        const user = await this.manager.signinRedirectCallback();
        if (this.isAuthenticated(user)) {
            this.loginSuccess(options, user);
            return;
        }
        this.loginFail(options);
    }

    /**
     * 登录成功处理
     * @param options OpenId Connect认证登陆回调配置
     * @param user 用户
     */
    private loginSuccess(options: IOidcLoginCallbackOptions, user: User) {
        options.handler && options.handler(user);
        const redirectUrl = options.redirectUrl || '/';
        bing.router.navigate([redirectUrl]);
    }

    /**
     * 登录失败处理
     * @param options OpenId Connect认证登陆回调配置
     */
    private loginFail(options: IOidcLoginCallbackOptions) {
        options.failHandler && options.failHandler();
        if (options.failRedirectUrl)
            bing.router.navigate([options.failRedirectUrl]);
    }

    /**
     * 获取用户信息
     */
    getUser() {
        return this.manager.getUser();
    }

    /**
     * 注销，重定向到认证服务器进行登出操作
     */
    logout() {
        this.manager.signoutRedirect();
    }
}

/**
 * OpenId Connect认证登录回调配置
 */
export interface IOidcLoginCallbackOptions {
    /**
     * 登录成功跳转地址，默认值：/
     */
    redirectUrl?: string;
    /**
     * 登录失败跳转地址
     */
    failRedirectUrl?: string;
    /**
     * 登录成功处理函数
     * @param user 用户信息
     */
    handler?: (user: User) => void;
    /**
     * 提交失败处理函数
     */
    failHandler?: () => void;
}
