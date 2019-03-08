using Newtonsoft.Json;

namespace XIVLauncher
{
    public partial class GitHubCommit
    {
        [JsonProperty("sha")]
        public string Sha { get; set; }

        public partial class GitHubCommitInfo
        {
            public class GitHubCommitAuthor
            {
                [JsonProperty("name")]
                public string Name { get; set; }

                [JsonProperty("email")]
                public string Email { get; set; }

                [JsonProperty("date")]
                public System.DateTime Date { get; set; }
            }
        
            [JsonProperty("author")]
            public GitHubCommitAuthor Author { get; set; }

            [JsonProperty("committer")]
            public GitHubCommitAuthor Committer { get; set; }

            [JsonProperty("message")]
            public string Message { get; set; }
        
            public class GitHubCommitTree
            {
                [JsonProperty("sha")]
                public string Sha { get; set; }

                [JsonProperty("url")]
                public string Url { get; set; }
            }

            [JsonProperty("tree")]
            public GitHubCommitTree Tree { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("comment_count")]
            public long CommentCount { get; set; }
        
            public class GitHubCommitVerification
            {
                [JsonProperty("verified")]
                public bool Verified { get; set; }

                [JsonProperty("reason")]
                public string Reason { get; set; }

                [JsonProperty("signature")]
                public string Signature { get; set; }

                [JsonProperty("payload")]
                public string Payload { get; set; }
            }

            [JsonProperty("verification")]
            public GitHubCommitVerification Verification { get; set; }
        }
        
        [JsonProperty("commit")]
        public GitHubCommitInfo CommitInfo { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("html_url")]
        public string HtmlUrl { get; set; }

        [JsonProperty("comments_url")]
        public string CommentsUrl { get; set; }
        
        public class GitHubUser
        {
            [JsonProperty("login")]
            public string Login { get; set; }

            [JsonProperty("id")]
            public long Id { get; set; }

            [JsonProperty("avatar_url")]
            public string AvatarUrl { get; set; }

            [JsonProperty("gravatar_id")]
            public string GravatarId { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("html_url")]
            public string HtmlUrl { get; set; }

            [JsonProperty("followers_url")]
            public string FollowersUrl { get; set; }

            [JsonProperty("following_url")]
            public string FollowingUrl { get; set; }

            [JsonProperty("gists_url")]
            public string GistsUrl { get; set; }

            [JsonProperty("starred_url")]
            public string StarredUrl { get; set; }

            [JsonProperty("subscriptions_url")]
            public string SubscriptionsUrl { get; set; }

            [JsonProperty("organizations_url")]
            public string OrganizationsUrl { get; set; }

            [JsonProperty("repos_url")]
            public string ReposUrl { get; set; }

            [JsonProperty("events_url")]
            public string EventsUrl { get; set; }

            [JsonProperty("received_events_url")]
            public string ReceivedEventsUrl { get; set; }

            [JsonProperty("type")]
            public string PurpleType { get; set; }

            [JsonProperty("site_admin")]
            public bool SiteAdmin { get; set; }
        }

        [JsonProperty("author")]
        public GitHubUser Author { get; set; }

        [JsonProperty("committer")]
        public GitHubUser Committer { get; set; }
        
        public partial class GitHubParent
        {
            [JsonProperty("sha")]
            public string Sha { get; set; }

            [JsonProperty("url")]
            public string Url { get; set; }

            [JsonProperty("html_url")]
            public string HtmlUrl { get; set; }
        }

        [JsonProperty("parents")]
        public GitHubParent[] GitHubParents { get; set; }
    }

    public partial class GitHubCommit
    {
        public static GitHubCommit[] FromJson(string json) => JsonConvert.DeserializeObject<GitHubCommit[]>(json, new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
        });
    }
}