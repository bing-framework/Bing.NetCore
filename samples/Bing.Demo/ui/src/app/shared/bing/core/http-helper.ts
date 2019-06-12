// ============== Http操作 =========================
// Copyright 2019 简玄冰
// Licensed under the MIT license
// ================================================
import { HttpClient, HttpHeaders, HttpParams, HttpErrorResponse } from '@angular/common/http';
import { IocHelper as ioc } from './ioc-helper';
import { formatDate } from '../common/helper';

/**
 * Http操作
 */
export class HttpHelper {

}

/**
 * Http请求操作
 */
export class HttpRequest<T> {
    /**
     * Http请求头集合
     */
    private headers: HttpHeaders;
    /**
     * 内容类型
     */
    private httpContentType: HttpContentType;
    /**
     * Http参数集合
     */
    private parameters: HttpParams;

    /**
     * 初始化Http请求操作
     * @param httpMethod Http方法
     * @param url 请求地址
     * @param body Http主体
     */
    constructor(private httpMethod: HttpMethod, private url: string, private body?) {
        this.headers = new HttpHeaders();
        this.parameters = new HttpParams();
    }

    /**
     * 添加Http请求头
     * @param name 名称
     * @param value 值
     */
    header(name: string, value): HttpRequest<T> {
        let stringValue = '';
        if (value !== undefined && value != null)
            stringValue = String(value);
        this.headers = this.headers.append(name, stringValue);
        return this;
    }

    /**
     * 设置内容类型
     * @param contentType 内容类型
     */
    contentType(contentType: HttpContentType): HttpRequest<T> {
        this.httpContentType = contentType;
        return this;
    }

    /**
     * 添加Http参数，添加到url查询字符串
     * @param data 参数对象
     */
    param(data): HttpRequest<T>;
    /**
     * 添加Http参数，添加到url查询字符串
     * @param name 名城管
     * @param value 值
     */
    param(name: string, value: string): HttpRequest<T>;
    param(data, value?: string): HttpRequest<T> {
        if (typeof data === 'object') {
            this.paramByObject(data);
            return this;
        }
        if (typeof data === 'string' && value)
            this.parameters = this.parameters.append(data, value);
        return this;
    }

    /**
     * 添加Http参数
     * @param data 数据
     */
    private paramByObject(data) {
        for (const key in data) {
            if (data.hasOwnProperty(key)) {
                let value = this.getValue(data[key]);
                if (value === null)
                    value = '';
                this.parameters = this.parameters.append(key, value);
            }
        }
    }

    /**
     * 获取值
     * @param item 数据
     */
    private getValue(item): string {
        if (!item)
            return item;
        if (item instanceof Date)
            return formatDate(item);
        return item;
    }
}

/**
 * Http方法
 */
export enum HttpMethod {
    Get,
    Post,
    Put,
    Delete
}

/**
 * Http内容类型
 */
export enum HttpContentType {
    /**
     * application/x-www-form-urlencoded
     */
    FormUrlEncoded,
    /**
     * application/json
     */
    Json
}
