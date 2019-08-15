import { Injector } from '@angular/core';
import { bing } from '../index';

/**
 * 组件基类
 */
export class ComponentBase {
    /**
     * 初始化组件
     * @param injector 注入器
     */
    constructor(injector: Injector) {
        bing.ioc.componentInjector = injector;
    }

    /**
     * 操作库
     */
    protected bing = bing;
}