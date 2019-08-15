// ============== 组件基类 =========================
// Copyright 2019 简玄冰
// Licensed under the MIT license
// ================================================
import { Injector } from '@angular/core';
import { bing } from '../index';

/**
 * 组件基类
 */
export class ComponentBase {
    /**
     * 操作库
     */
    protected bing = bing;

    /**
     * 初始化组件
     * @param injector 注入器
     */
    constructor(injector: Injector) {
        bing.ioc.componentInjector = injector;
    }
}
