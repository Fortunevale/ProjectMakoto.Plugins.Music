// Project Makoto
// Copyright (C) 2024  Fortunevale
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

using ProjectMakoto.Entities.Users;

namespace ProjectMakoto.Plugins.Music;

internal sealed class NewPlaylistCommand : BaseCommand
{
    public override Task ExecuteCommand(SharedCommandContext ctx, Dictionary<string, object> arguments)
    {
        var CommandKey = ((Entities.Translations)MusicPlugin.Plugin!.Translations).Commands.Music;

        return Task.Run(async () =>
        {
            if (await ctx.DbUser.Cooldown.WaitForModerate(ctx))
                return;

            var SelectedPlaylistName = "";
            PlaylistEntry[] SelectedTracks = null;

            while (true)
            {
                if (MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Length >= 10)
                {
                    _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
                    {
                        Description = this.GetString(CommandKey.Playlists.PlayListLimit, true, new TVar("Count", 10)),
                    }.AsError(ctx, this.GetString(CommandKey.Playlists.Title))));
                    await Task.Delay(5000);
                    return;
                }

                var SelectName = new DiscordButtonComponent((SelectedPlaylistName.IsNullOrWhiteSpace() ? ButtonStyle.Primary : ButtonStyle.Secondary), Guid.NewGuid().ToString(), this.GetString(CommandKey.Playlists.CreatePlaylist.ChangeName), false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("🗯")));
                var SelectFirstTracks = new DiscordButtonComponent((SelectedTracks is null ? ButtonStyle.Primary : ButtonStyle.Secondary), Guid.NewGuid().ToString(), this.GetString(CommandKey.Playlists.CreatePlaylist.ChangeTracks), false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("🎵")));
                var Finish = new DiscordButtonComponent(ButtonStyle.Success, Guid.NewGuid().ToString(), this.GetString(CommandKey.Playlists.CreatePlaylist.CreatePlaylist), (SelectedPlaylistName.IsNullOrWhiteSpace()), new DiscordComponentEmoji(DiscordEmoji.FromUnicode("✅")));

                var pad = TranslationUtil.CalculatePadding(ctx.DbUser, CommandKey.Playlists.CreatePlaylist.PlaylistName, CommandKey.Playlists.CreatePlaylist.FirstTracks);

                var embed = new DiscordEmbedBuilder
                {
                    Description = $"`{this.GetString(CommandKey.Playlists.CreatePlaylist.PlaylistName).PadRight(pad)}`: `{(SelectedPlaylistName.IsNullOrWhiteSpace() ? this.GetString(this.t.Common.NotSelected) : SelectedPlaylistName)}`\n" +
                                  $"`{this.GetString(CommandKey.Playlists.CreatePlaylist.FirstTracks).PadRight(pad)}`: {(SelectedTracks.IsNotNullAndNotEmpty() ? (SelectedTracks.Length > 1 ? $"`{SelectedTracks.Length} {this.GetString(CommandKey.Playlists.Tracks)}`" : $"[`{SelectedTracks[0].Title}`]({SelectedTracks[0].Url})") : this.GetString(this.t.Common.NotSelected, true))}"
                }.AsAwaitingInput(ctx, this.GetString(CommandKey.Playlists.Title));

                _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(embed)
                    .AddComponents(new List<DiscordComponent> { SelectName, SelectFirstTracks, Finish })
                    .AddComponents(MessageComponents.GetCancelButton(ctx.DbUser, ctx.Bot)));

                var Menu = await ctx.WaitForButtonAsync();

                if (Menu.TimedOut)
                {
                    this.ModifyToTimedOut();
                    return;
                }

                if (Menu.GetCustomId() == SelectName.CustomId)
                {
                    var modal = new DiscordInteractionModalBuilder(this.GetString(CommandKey.Playlists.CreatePlaylist.SetPlaylistName), Guid.NewGuid().ToString())
                    .AddTextComponent(new DiscordTextComponent(TextComponentStyle.Small, "name", this.GetString(CommandKey.Playlists.CreatePlaylist.PlaylistName), this.GetString(CommandKey.Playlists.Title), 1, 100, true, (SelectedPlaylistName.IsNullOrWhiteSpace() ? "New Playlist" : SelectedPlaylistName)));

                    var ModalResult = await this.PromptModalWithRetry(Menu.Result.Interaction, modal, new DiscordEmbedBuilder
                    {
                        Description = $"⚠ {this.GetString(CommandKey.Playlists.NameModerationNote, true)}",
                    }.AsAwaitingInput(ctx, this.GetString(CommandKey.Playlists.Title)), false);

                    if (ModalResult.TimedOut)
                    {
                        this.ModifyToTimedOut(true);
                        return;
                    }
                    else if (ModalResult.Cancelled)
                    {
                        continue;
                    }
                    else if (ModalResult.Errored)
                    {
                        throw ModalResult.Exception;
                    }

                    SelectedPlaylistName = ModalResult.Result.Interaction.GetModalValueByCustomId("name");
                    continue;
                }
                else if (Menu.GetCustomId() == SelectFirstTracks.CustomId)
                {
                    var modal = new DiscordInteractionModalBuilder(this.GetString(CommandKey.Playlists.CreatePlaylist.SetFirstTracks), Guid.NewGuid().ToString())
                        .AddTextComponent(new DiscordTextComponent(TextComponentStyle.Small, "query", this.GetString(CommandKey.Playlists.CreatePlaylist.SupportedAddType), "Url", 1, 100, true));


                    var ModalResult = await this.PromptModalWithRetry(Menu.Result.Interaction, modal, false);

                    if (ModalResult.TimedOut)
                    {
                        this.ModifyToTimedOut(true);
                        return;
                    }
                    else if (ModalResult.Cancelled)
                    {
                        continue;
                    }
                    else if (ModalResult.Errored)
                    {
                        throw ModalResult.Exception;
                    }

                    var query = ModalResult.Result.Interaction.GetModalValueByCustomId("query");

                    var (Tracks, oriResult, Continue) = await MusicModuleAbstractions.GetLoadResult(ctx, query);

                    if (!Continue || !Tracks.IsNotNullAndNotEmpty())
                        continue;

                    SelectedTracks = Tracks.Select(x => new PlaylistEntry
                    {
                        Title = x.Info.Title,
                        Url = x.Info.Uri.ToString(),
                        Length = x.Info.Length
                    }).ToArray();
                    continue;
                }
                else if (Menu.GetCustomId() == Finish.CustomId)
                {
                    _ = Menu.Result.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

                    if (MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Length >= 10)
                    {
                        _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
                        {
                            Description = this.GetString(CommandKey.Playlists.PlayListLimit, true, new TVar("Count", 10)),
                        }.AsError(ctx, this.GetString(CommandKey.Playlists.Title))));
                        await Task.Delay(5000);
                        return;
                    }

                    _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
                    {
                        Description = this.GetString(CommandKey.Playlists.CreatePlaylist.Creating, true),
                    }.AsLoading(ctx, this.GetString(CommandKey.Playlists.Title))));

                    var v = new UserPlaylist
                    {
                        PlaylistName = SelectedPlaylistName,
                        List = SelectedTracks
                    };

                    MusicPlugin.Plugin.Users[ctx.User.Id].Playlists = MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Add(v);

                    _ = await this.RespondOrEdit(new DiscordMessageBuilder().WithEmbed(new DiscordEmbedBuilder
                    {
                        Description = this.GetString(CommandKey.Playlists.CreatePlaylist.Created, true,
                            new TVar("Playlist", v.PlaylistName),
                            new TVar("Count", v.List.Length)),
                    }.AsSuccess(ctx, this.GetString(CommandKey.Playlists.Title))));
                    await Task.Delay(2000);
                    await new ModifyCommand().TransferCommand(ctx, new Dictionary<string, object>
                    {
                        { "playlist", v.PlaylistId }
                    });
                    return;
                }
                else if (Menu.GetCustomId() == MessageComponents.CancelButtonId)
                {
                    if (!ctx.Transferred)
                        this.DeleteOrInvalidate();

                    return;
                }

                return;
            }
        });
    }
}