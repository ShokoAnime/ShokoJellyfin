using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using ShokoJellyfin.Providers.API;
using EpisodeType = ShokoJellyfin.Providers.API.Models.Episode.EpisodeType;

namespace ShokoJellyfin.Providers
{
    public class EpisodeProvider: IRemoteMetadataProvider<Episode, EpisodeInfo>
    {
        public string Name => "Shoko";
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EpisodeProvider> _logger;

        public EpisodeProvider(IHttpClientFactory httpClientFactory, ILogger<EpisodeProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>();

            // TO-DO Check if it can be written in a better way. Parent directory + File Name
            var filename = Path.Join(Path.GetDirectoryName(info.Path)?.Split(Path.DirectorySeparatorChar).LastOrDefault(),
                Path.GetFileName(info.Path));
            
            _logger.LogInformation($"Shoko Scanner... Getting episode ID ({filename})");
            
            var apiResponse = await ShokoAPI.GetFilePathEndsWith(filename);
            var allIds = apiResponse.FirstOrDefault()?.SeriesIDs.FirstOrDefault()?.EpisodeIDs;
            var episodeIDs = allIds?.FirstOrDefault();
            var episodeId = episodeIDs?.ID.ToString();

            if (string.IsNullOrEmpty(episodeId))
            {
                _logger.LogInformation($"Shoko Scanner... Episode not found! ({filename})");
                return result;
            }
            
            _logger.LogInformation($"Shoko Scanner... Getting episode metadata ({filename} - {episodeId})");
            
            var episodeInfo = await ShokoAPI.GetEpisodeAniDb(episodeId);
            
            result.Item = new Episode
            {
                IndexNumber = episodeInfo.EpisodeNumber,
                ParentIndexNumber = GetSeasonNumber(episodeInfo.Type),
                Name = episodeInfo.Titles.Find(title => title.Language.Equals("EN"))?.Name,
                PremiereDate = episodeInfo.AirDate,
                Overview = Helper.SummarySanitizer(episodeInfo.Description),
                CommunityRating = (float)((episodeInfo.Rating.Value * 10) / episodeInfo.Rating.MaxValue),
            };
            result.Item.SetProviderId("Shoko", episodeId);
            result.Item.SetProviderId("AniDB", episodeIDs.AniDB.ToString());
            var tvdbId = episodeIDs.TvDB?.FirstOrDefault();
            if (tvdbId != 0) result.Item.SetProviderId("Tvdb", tvdbId.ToString());
            result.HasMetadata = true;

            var episodeNumberEnd = episodeInfo.EpisodeNumber + allIds.Count() - 1;
            if (episodeInfo.EpisodeNumber != episodeNumberEnd) result.Item.IndexNumberEnd = episodeNumberEnd;
            
            return result;
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            // Isn't called from anywhere. If it is called, I don't know from where.
            throw new NotImplementedException();
        }
        
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient().GetAsync(url, cancellationToken);
        }

        private int GetSeasonNumber(EpisodeType type)
        {
            switch (type)
            {
                case EpisodeType.Episode:
                    return 1;
                case EpisodeType.Credits:
                    return 100;
                case EpisodeType.Special:
                    return 0;
                case EpisodeType.Trailer:
                    return 99;
                default:
                    return 98;
            }
        }
    }
}