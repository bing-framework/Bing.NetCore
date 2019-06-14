// ============== Crud编辑组件基类===============
// Copyright 2019 简玄冰
// Licensed under the MIT license
// ================================================

import { FormComponentBase } from './form-component-base';
import { OnInit, Injector } from '@angular/core';
import { ViewModel, bing } from '../index';
import { NgForm } from '@angular/forms';

/**
 * Crud编辑组件基类
 */
export abstract class EditComponentBase<TViewModel extends ViewModel> extends FormComponentBase implements OnInit {
    /**
     * 操作库
     */
    protected bing = bing;
    /**
     * 视图模型
     */
    model: TViewModel;

    /**
     * 初始化组件
     * @param injector 注入器
     */
    constructor(injector: Injector) {
        super(injector);
        this.model = this.createModel();
    }

    /**
     * 创建视图模型
     */
    protected createModel(): TViewModel {
        return {} as TViewModel;
    }

    /**
     * 初始化
     */
    ngOnInit() {
        this.loadById();
    }

    /**
     * 通过标识加载
     * @param id 标识
     */
    protected loadById(id = null) {
        id = id || this.bing.router.getParam('id');
        if (!id)
            return;
        this.bing.webapi.get<TViewModel>(this.getByIdUrl(id)).handle({
            ok: result => {
                result = this.loadBefore(result);
                this.model = result;
                this.loadAfter(result);
            }
        });
    }

    /**
     * 加载完成前操作
     * @param result 结果
     */
    protected loadBefore(result) {
        return result;
    }

    /**
     * 加载完成后操作
     * @param result 结果
     */
    protected loadAfter(result) { }

    /**
     * 获取基地址
     */
    protected abstract getBaseUrl();

    /**
     * 获取单个实体地址
     * @param id 标识
     */
    protected getByIdUrl(id) {
        return `/api/${this.getBaseUrl()}/${id}`;
    }

    submit(form?: NgForm, button?) {
        this.bing.form.submit({
            url: this.getSubmitUrl(),
            data: this.model,
            form: form,
            button: button,
            back: true
        });
    }

    /**
     * 获取提交地址
     */
    protected getSubmitUrl() {
        return `/api/${this.getBaseUrl()}`;
    }

    /**
     * 返回
     */
    back() {
        this.bing.router.back();
    }
}
