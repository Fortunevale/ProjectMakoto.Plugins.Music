// Project Makoto
// Copyright (C) 2024  Fortunevale
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

namespace ProjectMakoto.Plugins.Music;

internal sealed class ManageCommand : BaseCommand
{
    public override Task ExecuteCommand(SharedCommandContext ctx, Dictionary<string, object> arguments)
    {
        var CommandKey = ((Entities.Translations)MusicPlugin.Plugin!.Translations).Commands.Music;

        return Task.Run(async () =>
        {
            if (await ctx.DbUser.Cooldown.WaitForModerate(ctx))
                return;

            var countInt = 0;

            int GetCount()
            {
                countInt++;
                return countInt;
            }

            var builder = new DiscordMessageBuilder().AddEmbed(new DiscordEmbedBuilder()
            {
                Description = $"{(MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Length > 0 ? string.Join("\n", MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Select(x => $"**{GetCount()}**. `{x.PlaylistName.SanitizeForCode()}`: `{x.List.Length} {this.GetString(CommandKey.Playlists.Tracks)}`")) : this.GetString(CommandKey.Playlists.Manage.NoPlaylists, true))}"
            }.AsAwaitingInput(ctx, this.GetString(CommandKey.Playlists.Title)));

            var AddToQueue = new DiscordButtonComponent(ButtonStyle.Success, Guid.NewGuid().ToString(), this.GetString(CommandKey.Playlists.Manage.AddToQueueButton), (MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Length <= 0), new DiscordComponentEmoji(DiscordEmoji.FromUnicode("📤")));
            var SharePlaylist = new DiscordButtonComponent(ButtonStyle.Primary, Guid.NewGuid().ToString(), this.GetString(CommandKey.Playlists.Manage.ShareButton), (MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Length <= 0), new DiscordComponentEmoji(DiscordEmoji.FromUnicode("📎")));
            var ExportPlaylist = new DiscordButtonComponent(ButtonStyle.Secondary, Guid.NewGuid().ToString(), this.GetString(CommandKey.Playlists.Manage.ExportButton), (MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Length <= 0), new DiscordComponentEmoji(DiscordEmoji.FromUnicode("📋")));

            var ImportPlaylist = new DiscordButtonComponent(ButtonStyle.Success, Guid.NewGuid().ToString(), this.GetString(CommandKey.Playlists.Manage.ImportButton), false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("📥")));
            var SaveCurrent = new DiscordButtonComponent(ButtonStyle.Success, Guid.NewGuid().ToString(), this.GetString(CommandKey.Playlists.Manage.SaveCurrentButton), false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("💾")));
            var NewPlaylist = new DiscordButtonComponent(ButtonStyle.Success, Guid.NewGuid().ToString(), this.GetString(CommandKey.Playlists.Manage.CreateNewButton), false, new DiscordComponentEmoji(DiscordEmoji.FromUnicode("➕")));
            var ModifyPlaylist = new DiscordButtonComponent(ButtonStyle.Primary, Guid.NewGuid().ToString(), this.GetString(CommandKey.Playlists.Manage.ModifyButton), (MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Length <= 0), new DiscordComponentEmoji(DiscordEmoji.FromUnicode("⚙")));
            var DeletePlaylist = new DiscordButtonComponent(ButtonStyle.Danger, Guid.NewGuid().ToString(), this.GetString(CommandKey.Playlists.Manage.DeleteButton), (MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Length <= 0), new DiscordComponentEmoji(DiscordEmoji.FromUnicode("🗑")));

            _ = await this.RespondOrEdit(builder
            .AddComponents(new List<DiscordComponent> {
                AddToQueue,
                SharePlaylist,
                ExportPlaylist
            })
            .AddComponents(new List<DiscordComponent>
            {
                ImportPlaylist,
                SaveCurrent,
                NewPlaylist
            })
            .AddComponents(new List<DiscordComponent>
            {
                ModifyPlaylist,
                DeletePlaylist
            })
            .AddComponents(MessageComponents.GetCancelButton(ctx.DbUser, ctx.Bot)));

            var e = await ctx.WaitForButtonAsync(TimeSpan.FromMinutes(1));

            if (e.TimedOut)
            {
                this.ModifyToTimedOut(true);
                return;
            }

            _ = e.Result.Interaction.CreateResponseAsync(InteractionResponseType.DeferredMessageUpdate);

            if (e.GetCustomId() == AddToQueue.CustomId)
            {
                _ = await this.RespondOrEdit(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.Playlists.Manage.PlaylistSelectorQueue, true)
                }.AsAwaitingInput(ctx, this.GetString(CommandKey.Playlists.Title)));

                var PlaylistResult = await this.PromptCustomSelection(GetPlaylistOptions());

                if (PlaylistResult.TimedOut)
                {
                    this.ModifyToTimedOut();
                    return;
                }
                else if (PlaylistResult.Cancelled)
                {
                    this.DeleteOrInvalidate();
                    return;
                }
                else if (PlaylistResult.Errored)
                {
                    throw PlaylistResult.Exception;
                }

                await new AddToQueueCommand().TransferCommand(ctx, new Dictionary<string, object>
                {
                    { "playlist", PlaylistResult.Result }
                });
                return;
            }
            else if (e.GetCustomId() == SharePlaylist.CustomId)
            {
                _ = await this.RespondOrEdit(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.Playlists.Manage.PlaylistSelectorShare, true)
                }.AsAwaitingInput(ctx, this.GetString(CommandKey.Playlists.Title)));

                var PlaylistResult = await this.PromptCustomSelection(GetPlaylistOptions());

                if (PlaylistResult.TimedOut)
                {
                    this.ModifyToTimedOut();
                    return;
                }
                else if (PlaylistResult.Cancelled)
                {
                    this.DeleteOrInvalidate();
                    return;
                }
                else if (PlaylistResult.Errored)
                {
                    throw PlaylistResult.Exception;
                }

                await new ShareCommand().TransferCommand(ctx, new Dictionary<string, object>
                {
                    { "playlist", PlaylistResult.Result }
                });
                return;
            }
            else if (e.GetCustomId() == ExportPlaylist.CustomId)
            {
                _ = await this.RespondOrEdit(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.Playlists.Manage.PlaylistSelectorExport, true)
                }.AsAwaitingInput(ctx, this.GetString(CommandKey.Playlists.Title)));

                var PlaylistResult = await this.PromptCustomSelection(GetPlaylistOptions());

                if (PlaylistResult.TimedOut)
                {
                    this.ModifyToTimedOut();
                    return;
                }
                else if (PlaylistResult.Cancelled)
                {
                    this.DeleteOrInvalidate();
                    return;
                }
                else if (PlaylistResult.Errored)
                {
                    throw PlaylistResult.Exception;
                }

                await new ExportCommand().TransferCommand(ctx, new Dictionary<string, object>
                {
                    { "playlist", PlaylistResult.Result }
                });
                return;
            }
            else if (e.GetCustomId() == NewPlaylist.CustomId)
            {
                await new NewPlaylistCommand().TransferCommand(ctx, null);
                return;
            }
            else if (e.GetCustomId() == SaveCurrent.CustomId)
            {
                await new SaveCurrentCommand().TransferCommand(ctx, null);
                return;
            }
            else if (e.GetCustomId() == ImportPlaylist.CustomId)
            {
                await new ImportCommand().TransferCommand(ctx, null);

                await this.ExecuteCommand(ctx, arguments);
                return;
            }
            else if (e.GetCustomId() == ModifyPlaylist.CustomId)
            {
                _ = await this.RespondOrEdit(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.Playlists.Manage.PlaylistSelectorModify, true)
                }.AsAwaitingInput(ctx, this.GetString(CommandKey.Playlists.Title)));

                var PlaylistResult = await this.PromptCustomSelection(GetPlaylistOptions());

                if (PlaylistResult.TimedOut)
                {
                    this.ModifyToTimedOut();
                    return;
                }
                else if (PlaylistResult.Cancelled)
                {
                    this.DeleteOrInvalidate();
                    return;
                }
                else if (PlaylistResult.Errored)
                {
                    throw PlaylistResult.Exception;
                }

                await new ModifyCommand().TransferCommand(ctx, new Dictionary<string, object>
                {
                    { "playlist", PlaylistResult.Result }
                });
                return;
            }
            else if (e.GetCustomId() == DeletePlaylist.CustomId)
            {
                _ = await this.RespondOrEdit(new DiscordEmbedBuilder
                {
                    Description = this.GetString(CommandKey.Playlists.Manage.PlaylistSelectorDelete, true)
                }.AsAwaitingInput(ctx, this.GetString(CommandKey.Playlists.Title)));

                var PlaylistResult = await this.PromptCustomSelection(GetPlaylistOptions());

                if (PlaylistResult.TimedOut)
                {
                    this.ModifyToTimedOut();
                    return;
                }
                else if (PlaylistResult.Cancelled)
                {
                    this.DeleteOrInvalidate();
                    return;
                }
                else if (PlaylistResult.Errored)
                {
                    throw PlaylistResult.Exception;
                }

                await new DeleteCommand().TransferCommand(ctx, new Dictionary<string, object>
                {
                    { "playlist", PlaylistResult.Result }
                });

                await this.ExecuteCommand(ctx, arguments);
                return;
            }
            else
            {
                this.DeleteOrInvalidate();
            }

            List<DiscordStringSelectComponentOption> GetPlaylistOptions()
            => MusicPlugin.Plugin.Users[ctx.User.Id].Playlists.Select(x => new DiscordStringSelectComponentOption($"{x.PlaylistName}", x.PlaylistId, $"{x.List.Length} {this.GetString(CommandKey.Playlists.Tracks)}")).ToList();
        });
    }
}