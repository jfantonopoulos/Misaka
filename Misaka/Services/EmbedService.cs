using Discord;
using System;
using System.Collections.Generic;
using System.Text;

namespace Misaka.Services
{
    public class EmbedService : Service
    {
        public EmbedService(IServiceProvider provider) : base(provider)
        {
        }

        protected override void Run()
        {
        }

        public Embed MakeSuccessFeedbackEmbed(string successMessage)
        {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithColor(new Color(100, 255, 100));
            embedBuilder.WithDescription($":ok_hand: {successMessage}");
            return embedBuilder.Build();
        }

        public Embed MakeFailFeedbackEmbed(string errorMessage)
        {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithColor(new Color(255, 100, 100));
            embedBuilder.WithDescription($":warning: {errorMessage}");
            return embedBuilder.Build();
        }

        public Embed MakeFeedbackEmbed(string feedbackMessage)
        {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.WithColor(new Color(175, 175, 175));
            embedBuilder.WithDescription($":information_source: {feedbackMessage}");
            return embedBuilder.Build();
        }

        public void BuildSuccessEmbed(EmbedBuilder embedBuilder)
        {
            embedBuilder.WithColor(new Color(100, 255, 100));
            embedBuilder.WithDescription($":ok_hand: ");
        }

        public void BuildFailEmbed(EmbedBuilder embedBuilder)
        {
            embedBuilder.WithColor(new Color(255, 100, 100));
            embedBuilder.WithDescription($":warning: ");
        }

        public void BuildFeedbackEmbed(EmbedBuilder embedBuilder)
        {
            embedBuilder.WithColor(new Color(175, 175, 175));
            embedBuilder.WithDescription($":information_source: ");
        }
    }
}
