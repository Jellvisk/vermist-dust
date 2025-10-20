namespace Content.Client._VDS.Chat.Managers;

public interface IClientOOCColorManager
{
    void Initialize();
    void HandleUpdateOOCColorMessage(Color color);
}
