using AlloyClient.Utils;
using Alloy.UiLib.Data;

namespace AlloyClient.Ui;

public static class GuildUtils {

    public const int Initiate = 0;
    public const int Member = 10;
    public const int Officer = 20;
    public const int Leader = 30;
    public const int Founder = 40;
    public const int MaxMembers = 50;

    public static TextureInfo? RankToIcon(int rank) {
        var index = rank switch {
            Initiate => 20,
            Member => 19,
            Officer => 18,
            Leader => 17,
            Founder => 16,
            _ => -1
        };

        if (index == -1) {
            return null;
        }

        return TextureHelper.FromGameAtlas("lofiInterfaceBig", index);
    }
}
