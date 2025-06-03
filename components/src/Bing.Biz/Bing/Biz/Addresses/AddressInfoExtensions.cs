﻿using Bing.Extensions;

namespace Bing.Biz.Addresses;

/// <summary>
/// 地址信息扩展
/// </summary>
public static class AddressInfoExtensions
{
    /// <summary>
    /// 转换为地址
    /// </summary>
    /// <param name="info">地址信息</param>
    public static Address ToAddress(this AddressInfo info) =>
        new Address(info.ProvinceId.ToGuidOrNull(), info.CityId.ToGuidOrNull(), info.CountyId.ToGuidOrNull(),
            info.Province, info.City, info.County, info.Street, info.Zip);

    /// <summary>
    /// 转换为地址信息
    /// </summary>
    /// <param name="address">地址</param>
    public static AddressInfo ToInfo(this Address address) =>
        new AddressInfo()
        {
            ProvinceId = address.ProvinceId.SafeString(),
            CityId = address.CityId.SafeString(),
            CountyId = address.CountyId.SafeString(),
            Province = address.Province,
            City = address.City,
            County = address.County,
            Street = address.Street,
            Zip = address.Zip,
            Description = address.Description
        };
}
