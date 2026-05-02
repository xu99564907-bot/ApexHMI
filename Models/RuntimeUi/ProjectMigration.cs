namespace ApexHMI.Models.RuntimeUi;

/// <summary>读时迁移钩子：将低版本 ProjectDocument 无损升级到当前版本。</summary>
public static class ProjectMigration
{
    public const int CurrentSchemaVersion = 1;

    /// <summary>对已反序列化的文档执行迁移；返回同一实例（已原地修改）。</summary>
    public static ProjectDocument Migrate(ProjectDocument doc)
    {
        // 预留：后续版本在此添加 if (doc.SchemaVersion < N) 迁移块
        doc.SchemaVersion = CurrentSchemaVersion;
        return doc;
    }
}
