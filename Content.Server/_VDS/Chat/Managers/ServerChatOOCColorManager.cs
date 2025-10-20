using Content.Server.Database;
using Content.Server.Preferences.Managers;
using Content.Shared._VDS.Preferences;
using Robust.Server.Player;
using Robust.Shared.Network;

namespace Content.Server._VDS.Chat.Managers;
/// <summary>
/// Manages incoming OOC Color messages, updating the color if valid.
/// </summary>
public sealed class OOCColorManager : IOOCColorManager
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IServerNetManager _netManager = default!;
    [Dependency] private readonly IServerPreferencesManager _prefsMan = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public void Initialize()
    {
        _netManager.RegisterNetMessage<MsgUpdateOOCColor>(HandleUpdateOOCColorMessage);
    }

    private async void HandleUpdateOOCColorMessage(MsgUpdateOOCColor message)
    {
        var userId = message.MsgChannel.UserId;
        var color = message.OOCColor;

        if (!_prefsMan.TryGetCachedPreferences(userId, out var prefsData))
        {
            return;
        }
        prefsData.OOCColor = Color.FromHex(color);
        var session = _playerManager.GetSessionById(userId);

        if (ShouldStorePrefs(session.Channel.AuthType))
            await _db.SaveOOCColorAsync(userId, Color.FromHex(color));
    }

    internal static bool ShouldStorePrefs(LoginType loginType)
    {
        return loginType.HasStaticUserId();
    }

}
