namespace ProxyMapService.Proxy.Configurations
{
    public class CacheRules
    {
        public List<CacheRule> Items { get; init; } = [];
        public List<CacheRulesFile> Files { get; init; } = [];

        public static void LoadRulesList(List<CacheRule> rules, IConfigurationSection configurationSection)
        {
            rules.Clear();
            List<IConfigurationRoot> fileConfigurations = [];
            var cacheRules = configurationSection.Get<CacheRules>();
            if (cacheRules != null)
            {
                rules.AddRange(cacheRules.Items);
                foreach (var file in cacheRules.Files)
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
                var addRules = fileConfig.GetSection("Items").Get<List<CacheRule>>();
                if (addRules != null)
                {
                    rules.AddRange(addRules);
                }
            }
        }
    }
}
