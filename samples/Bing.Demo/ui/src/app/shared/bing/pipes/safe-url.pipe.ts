// ============== 安全Url管道 =======================
// Copyright 2019 简玄冰
// Licensed under the MIT license
// ================================================
import { Pipe, PipeTransform } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';

@Pipe({
    name: 'safeUrl'
})
export class SafeUrlPipe implements PipeTransform {
    /**
     * 初始化安全Url管道
     */
    constructor(private sanitized: DomSanitizer) { }

    /**
     * 转换
     * @param value 值
     */
    transform(value) {
        return this.sanitized.bypassSecurityTrustResourceUrl(value);
    }
}
