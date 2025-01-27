// Project Makoto Example Plugin
// Copyright (C) 2023 Fortunevale
// This code is licensed under MIT license (see 'LICENSE'-file for details)

using DisCatSharp.Lavalink.Entities;
using DisCatSharp.Lavalink.Enums;
using DisCatSharp.Lavalink;
using DisCatSharp.Net;

namespace ProjectMakoto.Plugins.Music;

public class MusicPlugin : BasePlugin
{    public override string Name => "Music Commands";

    public override string Description => "This plugin provides music functionality for Makoto.";

    public override SemVer Version => new(1, 0, 0);

    public override int[] SupportedPluginApis => [1];

    public override string Author => "Mira";

    public override ulong? AuthorId => 411950662662881290;

    public override string UpdateUrl => "https://github.com/Fortunevale/ProjectMakoto.Plugins.Music";

    public override Octokit.Credentials? UpdateUrlCredentials => base.UpdateUrlCredentials;

    internal LavalinkSession LavalinkSession;

    public static MusicPlugin? Plugin { get; set; }

    public SelfFillingDatabaseDictionary<GuildMusic>? Guilds { get; set; } = null;
    public SelfFillingDatabaseDictionary<UserMusic>? Users { get; set; } = null;

    private PluginConfig LoadedConfig
    {
        get
        {
            if (!this.CheckIfConfigExists())
            {
                this._logger.LogDebug("Creating Plugin Config..");
                this.WriteConfig(new PluginConfig()); // You can use any class you choose, it gets saved with Newtonsoft.Json.
            }

            var v = this.GetConfig(); 

            if (v.GetType() == typeof(JObject))
            {
                this.WriteConfig(((JObject)v).ToObject<PluginConfig>() ?? new PluginConfig()); // Automatically converts the config you get to your PluginConfig.
                v = this.GetConfig();
            }

            return (PluginConfig)v;
        }
    }

    public override MusicPlugin Initialize()
    {
        bool Initialized = false;
        MusicPlugin.Plugin = this;

        var endpoint = new ConnectionEndpoint
        {
            Hostname = this.LoadedConfig.Host,
            Port = this.LoadedConfig.Port
        };

        var lavalinkConfig = new LavalinkConfiguration
        {
            Password = this.LoadedConfig.Password,
            RestEndpoint = endpoint,
            SocketEndpoint = endpoint
        };

        this.PreLogin += (s, e) =>
        {
            this._logger.LogInfo("Registering Lavalink..");
            _ = e.DiscordClient.UseLavalinkAsync().GetAwaiter().GetResult();
        };

        this.Connected += (s, e) =>
        {
            _ = Task.Run(async () =>
            {
                while (!this.Bot.status.DiscordInitialized)
                    await Task.Delay(100);

                try
                {
                    this._logger.LogInfo("Connecting and authenticating with Lavalink..");
                    this.LavalinkSession = await this.Bot.DiscordClient.GetFirstShard()!.GetLavalink().ConnectAsync(lavalinkConfig);
                    this._logger.LogInfo("Connected and authenticated with Lavalink.");
                    Initialized = true;

                    try
                    {
                        this._logger.LogInfo("Lavalink is running on {Version}.", (await this.LavalinkSession.GetLavalinkInfoAsync()).Version.Semver);
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    this._logger.LogError(ex, "An exception occurred while trying to log into Lavalink");
                    return;
                }
            });
        };

        this.DatabaseInitialized += (s, e) =>
        {
            this._logger.LogDebug("Initializing Dictionaries..");

            this.Guilds = new SelfFillingDatabaseDictionary<GuildMusic>(this, typeof(GuildMusic), (id) =>
            {
                return new GuildMusic(this, id);
            });

            this.Users = new SelfFillingDatabaseDictionary<UserMusic>(this, typeof(UserMusic), (id) =>
            {
                return new UserMusic(this, id);
            });
        };

        this.PreSyncTasksExecution += async (s, e) =>
        {
            this._logger.LogDebug("Importing user data from core..");

            foreach (var user in this.Bot.Users)
            {
                if (user.Value.LegacyUserPlaylists?.Length <= 0 || this.Users![user.Key].Playlists.Length > 0)
                    continue;

                this._logger.LogDebug("Importing playlists from '{User}'..", user.Key);

                this.Users![user.Key].Playlists = user.Value.LegacyUserPlaylists!.Select(x => new UserPlaylist
                {
                    List = x.List.Select(oldEntry => new PlaylistEntry
                    {
                        AddedTime = oldEntry.AddedTime,
                        Length = oldEntry.Length,
                        Title = oldEntry.Title,
                        Url = oldEntry.Url,
                    }).ToArray(),
                    PlaylistColor = x.PlaylistColor,
                    PlaylistId = x.PlaylistId,
                    PlaylistName = x.PlaylistName,
                    PlaylistThumbnail = x.PlaylistThumbnail,
                }).ToArray();

                user.Value.LegacyUserPlaylists = [];
            }
        };

        this.PostSyncTasksExecution += async (s, e) =>
        {
            while (!Initialized)
                await Task.Delay(100);

            var bot = (Bot)s!;

            Dictionary<string, TimeSpan> VideoLengthCache = new();

            foreach (var user in this.Users)
            {
                foreach (var list in user.Value.Playlists)
                {
                    foreach (var b in list.List.ToList())
                    {
                        if (b.Length is null || !b.Length.HasValue)
                        {
                            if (!VideoLengthCache.TryGetValue(b.Url, out var value))
                            {
                                this._logger.LogInfo("Fetching video length for '{Url}'", b.Url);

                                var loadResult = await bot.DiscordClient.GetFirstShard().GetLavalink().ConnectedSessions.First(x => x.Value.IsConnected).Value.LoadTracksAsync(LavalinkSearchType.Plain, b.Url);
                                var track = loadResult.GetResultAs<LavalinkTrack>();

                                if (loadResult.LoadType != LavalinkLoadResultType.Track)
                                {
                                    list.List = list.List.Remove(x => x.Url, b);
                                    this._logger.LogError("Failed to load video length for '{Url}'", b.Url);
                                    continue;
                                }

                                value = track.Info.Length;
                                VideoLengthCache.Add(b.Url, value);
                                await Task.Delay(100);
                            }

                            b.Length = value;
                        }
                    }
                }
            }

            foreach (var guild in e.Guilds)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (this.Guilds![guild.Id].ChannelId != 0)
                        {
                            if (!guild.Channels.ContainsKey(this.Guilds![guild.Id].ChannelId))
                                throw new Exception("Channel no longer exists");

                            if ((this.Guilds![guild.Id].CurrentVideo?.ToLower().Contains("localhost", StringComparison.CurrentCultureIgnoreCase) ?? false)
                                || (this.Guilds![guild.Id].CurrentVideo?.ToLower().Contains("127.0.0.1", StringComparison.CurrentCultureIgnoreCase) ?? false))
                                throw new Exception("Localhost?");

                            var channel = guild.GetChannel(this.Guilds![guild.Id].ChannelId);

                            if (!channel?.Users.Where(x => !x.IsBot).Any() ?? true)
                                throw new Exception("Channel empty");

                            if (this.Guilds![guild.Id].SongQueue.Length > 0)
                            {
                                for (var i = 0; i < this.Guilds![guild.Id].SongQueue.Length; i++)
                                {
                                    var b = this.Guilds![guild.Id].SongQueue[i];

                                    this._logger.LogDebug("Fixing queue info for '{Url}'", b.Url);
                                }
                            }

                            var lava = bot.DiscordClient.GetShard(guild.Id).GetLavalink();

                            while (!lava.ConnectedSessions.Values.Any(x => x.IsConnected))
                                await Task.Delay(1000);

                            var node = lava.ConnectedSessions.Values.First(x => x.IsConnected);
                            var conn = node.GetGuildPlayer(guild);

                            if (conn is null)
                            {
                                if (!lava.ConnectedSessions.Any())
                                {
                                    throw new Exception("Lavalink connection isn't established.");
                                }

                                conn = await node.ConnectAsync(channel);
                            }

                            var loadResult = await node.LoadTracksAsync(LavalinkSearchType.Plain, this.Guilds![guild.Id].CurrentVideo!);

                            if (loadResult.LoadType is LavalinkLoadResultType.Error or LavalinkLoadResultType.Empty)
                                return;

                            _ = await conn.PlayAsync(loadResult.GetResultAs<LavalinkTrack>());

                            await Task.Delay(1000);
                            _ = await conn.SeekAsync(TimeSpan.FromSeconds(this.Guilds![guild.Id].CurrentVideoPosition));

                            MusicPlugin.Plugin.Guilds![guild.Id].QueueHandler(bot, bot.DiscordClient.GetShard(guild.Id), node, conn);
                        }
                    }
                    catch (Exception ex)
                    {
                        this._logger.LogError(ex, "An exception occurred while trying to continue music playback for '{guild}'", guild.Id);
                        MusicPlugin.Plugin.Guilds![guild.Id].Reset();
                    }
                });

                await Task.Delay(1000);
            }
        };

        return this;
    }

    public override async Task<IEnumerable<MakotoModule>> RegisterCommands()
    {
        return
        [
            new MakotoModule("Music", [
                new MakotoCommand("music", "Allows to play music and change the current playback settings.",
                    new MakotoCommand("join", "The bot will join your channel if it's not already being used in this server.", typeof(JoinCommand))
                        .WithAliases("connect"),
                    new MakotoCommand("disconnect", "Starts a voting to disconnect the bot.", typeof(DisconnectCommand))
                        .WithAliases("dc", "leave")
                        .WithIsEphemeral(false),
                    new MakotoCommand("forcedisconnect", "Forces the bot to disconnect. `DJ` role or Administrator permissions required.", typeof(ForceDisconnectCommand))
                        .WithAliases("fdc", "forceleave", "fleave", "stop"),
                    new MakotoCommand("play", "Searches for a video and adds it to the queue. If given a direct url, adds it to the queue.", typeof(PlayCommand),
                        new MakotoCommandOverload(typeof(string), "search", "Search Query/Url")),
                    new MakotoCommand("pause", "Pause or unpause the current song.", typeof(PauseCommand))
                        .WithAliases("resume"),
                    new MakotoCommand("queue", "Displays the current queue.", typeof(QueueCommand)),
                    new MakotoCommand("removequeue", "Remove a song from the queue.", typeof(RemoveQueueCommand),
                        new MakotoCommandOverload(typeof(string), "video", "The Index or Video Title")
                            .WithAutoComplete(typeof(AutocompleteProviders.SongQueueAutocompleteProvider)))
                        .WithAliases("rq"),
                    new MakotoCommand("skip", "Starts a voting to skip the current song.", typeof(SkipCommand))
                        .WithIsEphemeral(false),
                    new MakotoCommand("forceskip", "Forces skipping of the current song. `DJ` role or Administrator permissions required.", typeof(ForceSkipCommand))
                        .WithAliases("fs", "fskip"),
                    new MakotoCommand("clearqueue", "Starts a voting to clear the current queue.", typeof(ClearQueueCommand))
                        .WithAliases("cq")
                        .WithIsEphemeral(false),
                    new MakotoCommand("forceclearqueue", "Forces clearing the current queue. `DJ` role or Administrator permissions required.", typeof(ForceClearQueueCommand))
                        .WithAliases("fcq"),
                    new MakotoCommand("shuffle", "Toggles shuffling of the current queue.", typeof(ShuffleCommand)),
                    new MakotoCommand("repeat", "Toggles repeating the current queue.", typeof(RepeatCommand)))
                    .WithAliases("m"),

                new MakotoCommand("playlists", "Allows you to manage your personal playlists.",
                    new MakotoCommand("manage", "Allows you to use and manage your playlists.", typeof(ManageCommand)),
                    new MakotoCommand("add-to-queue", "Adds a playlist to the current song queue.", typeof(AddToQueueCommand),
                        new MakotoCommandOverload(typeof(string), "playlist", "The Playlist Id")
                            .WithAutoComplete(typeof(AutocompleteProviders.PlaylistsAutoCompleteProvider)))
                        .WithSupportedCommandTypes(MakotoCommandType.SlashCommand),
                    new MakotoCommand("share", "Share one of your playlists.", typeof(ShareCommand),
                        new MakotoCommandOverload(typeof(string), "playlist", "The Playlist Id")
                            .WithAutoComplete(typeof(AutocompleteProviders.PlaylistsAutoCompleteProvider)))
                        .WithSupportedCommandTypes(MakotoCommandType.SlashCommand),
                    new MakotoCommand("export", "Export one of your playlists.", typeof(ExportCommand),
                        new MakotoCommandOverload(typeof(string), "playlist", "The Playlist Id")
                            .WithAutoComplete(typeof(AutocompleteProviders.PlaylistsAutoCompleteProvider)))
                        .WithSupportedCommandTypes(MakotoCommandType.SlashCommand),
                    new MakotoCommand("modify", "Modify one of your playlists.", typeof(ModifyCommand),
                        new MakotoCommandOverload(typeof(string), "playlist", "The Playlist Id")
                            .WithAutoComplete(typeof(AutocompleteProviders.PlaylistsAutoCompleteProvider)))
                        .WithSupportedCommandTypes(MakotoCommandType.SlashCommand),
                    new MakotoCommand("delete", "Delete one of your playlists.", typeof(DeleteCommand),
                        new MakotoCommandOverload(typeof(string), "playlist", "The Playlist Id")
                            .WithAutoComplete(typeof(AutocompleteProviders.PlaylistsAutoCompleteProvider)))
                        .WithSupportedCommandTypes(MakotoCommandType.SlashCommand),
                    new MakotoCommand("create-new", "Create a new playlist from scratch.", typeof(NewPlaylistCommand)),
                    new MakotoCommand("save-queue", "Save the current queue as playlist.", typeof(SaveCurrentCommand)),
                    new MakotoCommand("import", "Import a playlists from another platform or from a previously exported playlist.", typeof(ImportCommand)),
                    new MakotoCommand("load-share", "Loads a playlist share.", typeof(LoadShareCommand),
                        new MakotoCommandOverload(typeof(DiscordUser), "user", "The user"),
                        new MakotoCommandOverload(typeof(string), "id", "The Id"))),
            ]).WithPriority(997)
        ];
    }

    public override Task<IEnumerable<Type>?> RegisterTables()
    {
        return Task.FromResult<IEnumerable<Type>?>(
        [
            typeof(GuildMusic),
            typeof(UserMusic)
        ]);
    }

    public override (string? path, Type? type) LoadTranslations()
    {
        return ("Translations/strings.json", typeof(Entities.Translations));
    }                                                                       
    
    public override Task Shutdown()
        => base.Shutdown();
}
