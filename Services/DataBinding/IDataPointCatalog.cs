using System.Collections.Generic;
using ApexHMI.Models;

namespace ApexHMI.Services.DataBinding;

/// <summary>数据点目录接口：将逻辑 TagId 映射到物理 TagItem。</summary>
public interface IDataPointCatalog
{
    TagItem? FindTag(string tagId);
    IEnumerable<TagItem> GetAll();
    void Merge(IEnumerable<TagItem> tags);
}
