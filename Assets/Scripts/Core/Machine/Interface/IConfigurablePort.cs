namespace AssemblyLine.Data.Machine
{
    /// <summary>
    /// 端口配置特征接口。
    /// 任何实现了该接口的模块，均可在 UI 层被呼出“白名单/规则设置”面板。
    /// </summary>
    public interface IConfigurablePort
    {
        PortRuleConfig GetRules();
    }
}