using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using SquidDraftLeague.Bot.Extensions.Entities;

namespace SquidDraftLeague.Bot.Extensions
{
    public static class DiscordAPIExtensions
    {
        public static string ToUserMention(this ulong id)
        {
            return $"<@{id}>";
        }

        public static DiscordEmbedBuilder WithColor(this DiscordEmbedBuilder builder, Color color)
        {
            return builder.WithColor(new DiscordColor(color.R, color.G, color.B));
        }

        public static DiscordEmbedBuilder AddField(this DiscordEmbedBuilder builder, Action<DiscordFieldBuilder> action)
        {
            DiscordFieldBuilder fieldBuilder = new DiscordFieldBuilder();
            action(fieldBuilder);
            return builder.AddField(fieldBuilder);
        }

        public static DiscordEmbedBuilder AddField(this DiscordEmbedBuilder builder, DiscordFieldBuilder fieldBuilder)
        {
            return builder.AddField(fieldBuilder.Name, fieldBuilder.Value.ToString(), fieldBuilder.IsInline);
        }

        public static DiscordEmbedBuilder WithFields(this DiscordEmbedBuilder builder,
            IEnumerable<DiscordFieldBuilder> fieldBuilders)
        {
            foreach (DiscordFieldBuilder discordFieldBuilder in fieldBuilders)
            {
                builder.AddField(discordFieldBuilder);
            }

            return builder;
        }

        public static async Task RemoveRolesAsync(this DiscordMember member, IEnumerable<DiscordRole> roles)
        {
            foreach (DiscordRole discordRole in roles)
            {
                await member.RevokeRoleAsync(discordRole);
            }
        }
    }
}
