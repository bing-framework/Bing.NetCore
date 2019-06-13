// ============== 路由操作 =========================
// Copyright 2019 简玄冰
// Licensed under the MIT license
// ================================================
import { Router, ActivatedRoute, NavigationExtras } from '@angular/router';
import { Location } from '@angular/common';
import { IocHelper as ioc } from './ioc-helper';

/**
 * 路由操作
 */
export class RouterHelper {
    /**
     * 返回上一次视图
     */
    static back(): void {
        const location: Location = ioc.get(Location);
        location.back();
    }

    /**
     * 导航
     * @param commands 导航参数，范例: ['team', 33, 'user', 11]，表示 /team/33/user/11
     * @param extras 附加参数，范例: {queryParams: {id:'1'}}，表示 ?id=1
     */
    static navigate(commands: any[], extras?: NavigationExtras): Promise<boolean> {
        const router: Router = ioc.get(Router);
        return router.navigate(commands, extras);
    }

    /**
     * 导航
     * @param commands 导航参数，范例: ['team', 33, 'user', 11]，表示 /team/33/user/11
     * @param queryParams 查询参数，范例: {id:'1'}，表示 ?id=1
     */
    static navigateByQuery(commands: any[], queryParams): Promise<boolean> {
        return this.navigate(commands, { queryParams });
    }

    /**
     * 导航
     * @param url 地址
     * @param extras 附加参数，范例: {queryParams: {id:'1'}}，表示 ?id=1
     */
    static navigateByUrl(url: string, extras?: NavigationExtras): Promise<boolean> {
        const router: Router = ioc.get(Router);
        return router.navigateByUrl(url, extras);
    }

    /**
     * 获取路径参数值，从路由快照中获取参数
     * @param paramName 参数名
     */
    static getParam(paramName: string): string | null {
        const router: ActivatedRoute = ioc.get(ActivatedRoute);
        return router.snapshot.paramMap.get(paramName);
    }

    /**
     * 获取查询参数值，从路由快照中的查询字符串获取参数
     * @param paramName 参数名
     */
    static getQueryParam(paramName: string): string | null {
        const router: ActivatedRoute = ioc.get(ActivatedRoute);
        return router.snapshot.queryParamMap.get(paramName);
    }
}
