// Project Makoto
// Copyright (C) 2024  Fortunevale
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

using DisCatSharp.Lavalink.Entities;
using DisCatSharp.Lavalink.Enums;
using ProjectMakoto.Entities.Users;

namespace ProjectMakoto.Plugins.Music;

internal sealed class ImportCommand : BaseCommand
{
    public override Task ExecuteCommand(SharedCommandContext ctx, Dictionary<string, object> arguments)
    {
        var CommandKey = ((Entities.Translations)MusicPlugin.Plugin!.Translations).Commands.Music;

        return Task.Run(async () =>
        {
            if (await ctx.DbUser.Cooldown.WaitForModerate(ctx))
                return;

            if (MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Length >= 10)
            {
                _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.Playlists.PlayListLimit, true, new TVar("Count", 10)),
                }.AsError(ctx, this.GetString(CommandKey.Playlists.Title))));
                return;
            }

            var Link = new DiscordButtonComponent(ButtonStyle.Primary, Guid.NewGuid().ToString(), this.GetString(CommandKey.Playlists.Import.Link), false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("↘")));
            var ExportedPlaylist = new DiscordButtonComponent(ButtonStyle.Primary, Guid.NewGuid().ToString(), this.GetString(CommandKey.Playlists.Import.ExportedPlaylist), false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("📂")));

            _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
            {
                Description = this.GetString(CommandKey.Playlists.Import.ImportMethod, true),
            }.AsAwaitingInput(ctx, this.GetString(CommandKey.Playlists.Title)))
            .AddComponents(new List<DiscordComponent> { Link, ExportedPlaylist })
            .AddComponents(MessageComponents.GetCancelButton(ctx.DbUser, ctx.Bot)));

            var Menu = await ctx.WaitForButtonAsync();

            if (Menu.TimedOut)
            {
                this.ModifyToTimedOut();
                return;
            }

            if (Menu.GetCustomId() == Link.CustomId)
            {
                var modal = new DiscordInteractionModalBuilder(this.GetString(CommandKey.Playlists.Import.ImportPlaylist), Guid.NewGuid().ToString())
                        .AddTextComponent(new DiscordTextComponent(TextComponentStyle.Small, "query", this.GetString(CommandKey.Playlists.Import.PlaylistUrl), "", 1, 100, true));

                var ModalResult = await this.PromptModalWithRetry(Menu.Result.Interaction, modal, false);

                if (ModalResult.TimedOut)
                {
                    this.ModifyToTimedOut(true);
                    return;
                }
                else if (ModalResult.Cancelled)
                {
                    return;
                }
                else if (ModalResult.Errored)
                {
                    throw ModalResult.Exception;
                }

                var query = ModalResult.Result.Interaction.GetModalValueByCustomId("query");

                var lava = ctx.Client.GetLavalink();
                var node = lava.ConnectedSessions.Values.First(x => x.IsConnected);

                if (Regex.IsMatch(query, "{jndi:(ldap[s]?|rmi):\\/\\/[^\n]+"))
                    throw new Exception();

                var loadResult = await node.LoadTracksAsync(LavalinkSearchType.Plain, query);

                if (loadResult.LoadType == LavalinkLoadResultType.Error)
                {
                    _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Description = this.GetString(CommandKey.Playlists.Import.NotLoaded, true),
                    }.AsError(ctx, this.GetString(CommandKey.Playlists.Title))));
                    return;
                }
                else if (loadResult.LoadType == LavalinkLoadResultType.Playlist)
                {
                    var playlistResult = loadResult.GetResultAs<LavalinkPlaylist>();

                    if (MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Length >= 10)
                    {
                        _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                        {
                            Description = this.GetString(CommandKey.Playlists.PlayListLimit, true, new TVar("Count", 10)),
                        }.AsError(ctx, this.GetString(CommandKey.Playlists.Title))));
                        return;
                    }

                    _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Description = this.GetString(CommandKey.Playlists.Import.Creating, true),
                    }.AsLoading(ctx, this.GetString(CommandKey.Playlists.Title))));

                    var v = new UserPlaylist
                    {
                        PlaylistName = playlistResult.Info.Name,
                        List = playlistResult.Tracks.Select(x => new PlaylistEntry { Title = x.Info.Title, Url = x.Info.Uri.ToString(), Length = x.Info.Length }).Take(250).ToArray()
                    };

                    MusicPlugin.Plugin.Users[ctx.User.Id].Playlists = MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Add(v);

                    _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Description = this.GetString(CommandKey.Playlists.Import.Created, true,
                        new TVar("Name", v.PlaylistName),
                        new TVar("Count", v.List.Length)),
                    }.AsSuccess(ctx, this.GetString(CommandKey.Playlists.Title))));
                    await Task.Delay(5000);
                    return;
                }
                else
                {
                    _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Description = this.GetString(CommandKey.Playlists.Import.NotLoaded, true),
                    }.AsError(ctx, this.GetString(CommandKey.Playlists.Title))));
                    return;
                }
            }
            else if (Menu.GetCustomId() == ExportedPlaylist.CustomId)
            {
                try
                {
                    _ = Menu.Result.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

                    _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Description = this.GetString(CommandKey.Playlists.Import.UploadExport, true, new TVar("Command", $"{ctx.Prefix}upload")),
                    }.AsAwaitingInput(ctx, this.GetString(CommandKey.Playlists.Title))));

                    Stream stream;

                    try
                    {
                        stream = (await this.PromptForFileUpload()).stream;
                    }
                    catch (AlreadyAppliedException)
                    {
                        return;
                    }
                    catch (ArgumentException)
                    {
                        this.ModifyToTimedOut();
                        return;
                    }

                    _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Description = this.GetString(CommandKey.Playlists.Import.Importing, true),
                    }.AsLoading(ctx, this.GetString(CommandKey.Playlists.Title))));

                    var rawJson = new StreamReader(stream).ReadToEnd();

                    var ImportJson = JsonConvert.DeserializeObject<UserPlaylist>((rawJson is null or "null" or "" ? "[]" : rawJson), new JsonSerializerSettings() { MissingMemberHandling = MissingMemberHandling.Error });

                    ImportJson.List = ImportJson.List.Where(x => RegexTemplates.Url.IsMatch(x.Url)).Select(x => new PlaylistEntry { Title = x.Title, Url = x.Url, Length = x.Length }).Take(250).ToArray();

                    if (ImportJson.List.Length == 0)
                        throw new Exception();

                    if (MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Length >= 10)
                    {
                        _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                        {
                            Description = this.GetString(CommandKey.Playlists.PlayListLimit, true, new TVar("Count", 10)),
                        }.AsError(ctx, this.GetString(CommandKey.Playlists.Title))));
                        return;
                    }

                    var v = new UserPlaylist
                    {
                        PlaylistName = ImportJson.PlaylistName,
                        List = ImportJson.List,
                        PlaylistColor = ImportJson.PlaylistColor
                    };

                    MusicPlugin.Plugin.Users[ctx.User.Id].Playlists = MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Add(v);

                    _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Description = this.GetString(CommandKey.Playlists.Import.Created, true,
                        new TVar("Name", v.PlaylistName),
                        new TVar("Count", v.List.Length)),
                    }.AsSuccess(ctx, this.GetString(CommandKey.Playlists.Title))));
                    await Task.Delay(5000);
                    return;
                }
                catch (Exception ex)
                {
                    _ = await this.RespondOrEdit(new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder
                    {
                        Description = this.GetString(CommandKey.Playlists.Import.ImportFailed, true),
                    }.AsError(ctx, this.GetString(CommandKey.Playlists.Title))));

                    MusicPlugin.Plugin!._logger.LogError(ex, "Failed to import a playlist");

                    return;
                }
            }
            else if (Menu.GetCustomId() == MessageComponents.CancelButtonId)
            {
                _ = Menu.Result.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);
                return;
            }
        });
    }
}