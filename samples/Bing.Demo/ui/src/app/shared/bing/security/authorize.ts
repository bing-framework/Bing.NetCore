// ============== 授权 =============================
// Copyright 2019 简玄冰
// Licensed under the MIT license
// ================================================
import { Injectable, Injector } from '@angular/core';
import { CanActivate } from '@angular/router/src/utils/preactivation';
import { Session } from './session';
import { bing } from '../index';
import { ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';

/**
 * 授权
 */
@Injectable()
export class Authorize implements CanActivate {
    /**
     * 登录地址，默认值 "/login"
     */
    static loginUrl = '/login';
    /**
     * 获取用户会话地址，默认值 "/api/security/session"
     */
    static sessionUrl = '/api/security/session';

    /**
     * 初始化授权
     * @param injector 注入器
     * @param session 用户会话
     */
    constructor(injector: Injector, private session: Session) {
        bing.ioc.componentInjector = injector;
    }

    /**
     * 是否激活组件
     * @param route 路由
     * @param state 状态
     */
    async canActivate(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Promise<boolean> {
        if (this.session && this.session.isAuthenticated)
            return true;
        await this.loadSessionAsync();
        if (this.session && this.session.isAuthenticated)
            return true;
        bing.router.navigateByQuery([Authorize.loginUrl], { returnUrl: state.url });
        return false;
    }

    /**
     * 加载用户会话
     */
    private async loadSessionAsync() {
        await bing.webapi.get(Authorize.sessionUrl).handleAsync({
            ok: (result: any) => {
                if (!result)
                    return;
                this.session.isAuthenticated = result.isAuthenticated;
                this.session.userId = result.userId;
            }
        });
    }
}
