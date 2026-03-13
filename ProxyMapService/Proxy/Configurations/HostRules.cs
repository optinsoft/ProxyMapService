namespace ProxyMapService.Proxy.Configurations
{
    public class HostRules
    {
        public List<HostRule> Items { get; init; } = [];
        public List<HostRulesFile> Files { get; init; } = [];

        private static void LoadRulesListItems(List<HostRule> rules, IConfigurationSection itemsSection)
        {
            foreach (var child in itemsSection.GetChildren())
            {
                var rule = child.Get<HostRule>();
                if (rule != null)
                {
                    bool cacheRulesExists = child.GetChildren().Any(s => s.Key == "CacheRules");
                    if (cacheRulesExists)
                    {
                        rule.LoadCacheRules(child.GetSection("CacheRules"));
                    }
                    rules.Add(rule);
                }
            }
        }

        public static void LoadRulesList(List<HostRule> rules, IConfigurationSection configurationSection)
        {
            rules.Clear();
            List<IConfigurationRoot> fileConfigurations = [];
            var itemsSection = configurationSection.GetSection("Items");
            LoadRulesListItems(rules, itemsSection);
            var filesSection = configurationSection.GetSection("Files");
            foreach(var child in filesSection.GetChildren())
            {
                var file = child.Get<HostRulesFile>();
                if (file != null)
                {
                    var fileConfig = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile(file.Path, optional: false)
                        .Build();
                    fileConfigurations.Add(fileConfig);
                }
            }
            foreach (var fileConfig in fileConfigurations)
            {
                var fileItemsSection = fileConfig.GetSection("Items");
                LoadRulesListItems(rules, fileItemsSection);
            }
        }
    }
}
