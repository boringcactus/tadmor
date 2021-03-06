﻿using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Tadmor.Extensions;
using Tadmor.Services.Sonagen;

namespace Tadmor.Modules
{
    [Summary("fursona generator")]
    [Group("sona")]
    [Name(nameof(SonagenModule))]
    public class SonagenModule : ModuleBase<ICommandContext>
    {
        private static readonly Random Random = new Random();
        private readonly SonagenService _sonagen;

        public SonagenModule(SonagenService sonagen)
        {
            _sonagen = sonagen;
        }

        private Task GenerateSona(Random random, IUser user = default, string seed = default)
        {
            var sona = _sonagen.GenerateSona(random);
            var builder = new EmbedBuilder();
            builder
                .WithDescription(sona.Description)
                .WithTitle($"{sona.Species} • {sona.Gender}");
            if (user != null) builder.WithAuthor(user);
            if (seed != null) builder.WithAuthor(seed);
            return ReplyAsync(string.Empty, embed: builder.Build());
        }

        [Summary("get your sona")]
        [Command]
        public Task GenerateUserSona()
        {
            return GenerateRandomSona((IGuildUser) Context.User);
        }

        [Summary("get a random sona")]
        [Command("random")]
        public Task GenerateRandomSona()
        {
            return GenerateSona(Random);
        }

        [Summary("get a sona for the specified name")]
        [Command]
        [Priority(-2)]
        public Task GenerateRandomSona([Remainder] string seed)
        {
            return GenerateSona(seed.ToRandom(), seed: seed);
        }
        [Summary("get a sona for the specified user")]
        [Command]
        [Priority(-1)]
        public Task GenerateRandomSona(IGuildUser user)
        {
            var random = (user.Nickname, user.AvatarId).ToRandom();
            return GenerateSona(random, user);
        }
    }
}