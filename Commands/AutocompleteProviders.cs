using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;

namespace ProjectMakoto.Plugins.Music;

public class AutocompleteProviders
{
    public sealed class SongQueueAutocompleteProvider : IAutocompleteProvider
    {
        public async Task<IEnumerable<DiscordApplicationCommandAutocompleteChoice>> Provider(AutocompleteContext ctx)
        {
            try
            {
                if ((ctx.Member?.VoiceState?.Channel?.Id ?? 0) != ((await ctx.Client.CurrentUser.ConvertToMember(ctx.Guild)).VoiceState?.Channel?.Id ?? 1))
                    return new List<DiscordApplicationCommandAutocompleteChoice>().AsEnumerable();

                var Queue = MusicPlugin.Plugin.Guilds![ctx.Guild.Id].SongQueue
                    .Where(x => x.VideoTitle.StartsWith(ctx.FocusedOption.Value.ToString(), StringComparison.InvariantCultureIgnoreCase)).Take(25);

                var options = Queue.Select(x => new DiscordApplicationCommandAutocompleteChoice(x.VideoTitle, x.VideoTitle)).ToList();
                return options.AsEnumerable();
            }
            catch (Exception ex)
            {
                MusicPlugin.Plugin._logger.LogError(ex, "Failed to provide autocomplete for song queue");
                return new List<DiscordApplicationCommandAutocompleteChoice>().AsEnumerable();
            }
        }
    }

    public sealed class PlaylistsAutoCompleteProvider : IAutocompleteProvider
    {
        public async Task<IEnumerable<DiscordApplicationCommandAutocompleteChoice>> Provider(AutocompleteContext ctx)
        {
            var CommandKey = ((Entities.Translations)MusicPlugin.Plugin!.Translations).Commands.Music;

            try
            {
                var bot = ((Bot)ctx.Services.GetService(typeof(Bot)));
                var Queue = MusicPlugin.Plugin.Users[ctx.User.Id].Playlists
                    .Where(x => x.PlaylistName.Contains(ctx.FocusedOption.Value.ToString(), StringComparison.InvariantCultureIgnoreCase)).Take(25);

                var options = Queue.Select(x => new DiscordApplicationCommandAutocompleteChoice($"{x.PlaylistName} ({x.List.Length} {CommandKey.Playlists.Tracks.Get(bot.Users[ctx.User.Id]).Build()})", x.PlaylistId)).ToList();
                return options.AsEnumerable();
            }
            catch (Exception ex)
            {
                MusicPlugin.Plugin._logger.LogError(ex, "Failed to provide autocomplete for playlists");
                return new List<DiscordApplicationCommandAutocompleteChoice>().AsEnumerable();
            }
        }
    }
}
