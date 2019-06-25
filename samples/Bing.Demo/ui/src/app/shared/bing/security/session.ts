// ============== 用户会话 ========================
// Copyright 2019 简玄冰
// Licensed under the MIT license
// ================================================
import { Injectable } from '@angular/core';
import { uuid } from '../common/helper';

@Injectable()
export class Session {
    /**
     * 会话标识
     */
    sessionId;
    /**
     * 是否认证
     */
    isAuthenticated;
    /**
     * 用户标识
     */
    userId;
    /**
     * 用户名称
     */
    name;

    /**
     * 初始化
     */
    constructor() {
        this.sessionId = uuid();
    }
}
