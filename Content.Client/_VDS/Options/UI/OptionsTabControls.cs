using Content.Client._VDS.Chat.Managers;
using Content.Client.Lobby;
using Content.Client.Options.UI;
using Robust.Client;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Client._VDS.Options.UI;

public sealed partial class OptionOOCColorSlider : BaseOptionCVar<string>
{
    private readonly IConfigurationManager _cfg;
    private readonly CVarDef<string> _cVar;
    private readonly OptionColorSlider _slider;
    protected override string Value
    {
        get => _slider.Slider.Color.ToHex();
        set
        {
            _slider.Slider.Color = Color.FromHex(value);
            UpdateLabelColor();
        }
    }
    public OptionOOCColorSlider(
        OptionsTabControlRow controller,
        IConfigurationManager cfg,
        CVarDef<string> cVar,
        OptionColorSlider slider) : base(controller, cfg, cVar)
    {
        _cfg = cfg;
        _cVar = cVar;
        _slider = slider;

        slider.Slider.OnColorChanged += _ =>
        {
            ValueChanged();
            UpdateLabelColor();
        };
    }

    public override void SaveValue()
    {
        // First save the CVar value
        _cfg.SetCVar(_cVar, _slider.Slider.Color.ToHex());

        var netManager = IoCManager.Resolve<IClientNetManager>();

        // Only attempt to update server if we're connected
        if (!netManager.IsConnected || netManager.ServerChannel == null)
            return;
        try
        {
            // Resolve the manager directly instead of using a field injection
            var oocColorManager = IoCManager.Resolve<IClientOOCColorManager>();
            oocColorManager.HandleUpdateOOCColorMessage(
                _slider.Slider.Color);
        }
        catch (Exception e)
        {
            IoCManager.Resolve<ISawmill>().Error($"Error updating OOC color on server: {e}");
        }
    }

    private void UpdateLabelColor()
    {

        if (_slider.ExampleLabel != null)
            _slider.ExampleLabel.FontColorOverride = _slider.Slider.Color;
    }
}
