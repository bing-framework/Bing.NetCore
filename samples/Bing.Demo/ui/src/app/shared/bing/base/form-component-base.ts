// ============== 表单编辑组件基类 ===============
// Copyright 2019 简玄冰
// Licensed under the MIT license
// ================================================
import { ViewChild, forwardRef, Injector } from '@angular/core';
import { NgForm } from '@angular/forms';
import { bing } from '..';

/**
 * 表单编辑组件基类
 */
export abstract class FormComponentBase {
    /**
     * 操作库
     */
    protected bing = bing;
    /**
     * 表单
     */
    @ViewChild(forwardRef(() => NgForm)) protected form: NgForm;

    /**
     * 初始化表单编辑组件基类
     * @param injector 注入器
     */
    constructor(public injector: Injector) {
        bing.ioc.componentInjector = injector;
    }
}