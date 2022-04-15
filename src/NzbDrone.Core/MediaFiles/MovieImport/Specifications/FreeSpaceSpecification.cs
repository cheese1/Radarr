using System;
using System.Collections.Generic;
using System.IO;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.MovieImport.Specifications
{
    public class FreeSpaceSpecification : IImportDecisionEngineSpecification
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public FreeSpaceSpecification(IDiskProvider diskProvider, IConfigService configService, Logger logger)
        {
            _diskProvider = diskProvider;
            _configService = configService;
            _logger = logger;
        }

        public IEnumerable<Decision> IsSatisfiedBy(LocalMovie localMovie, DownloadClientItem downloadClientItem)
        {
            return new List<Decision> { Calculate(localMovie, downloadClientItem) };
        }

        private Decision Calculate(LocalMovie localMovie, DownloadClientItem downloadClientItem)
        {
            if (_configService.SkipFreeSpaceCheckWhenImporting)
            {
                _logger.Debug("Skipping free space check when importing");
                return Decision.Accept();
            }

            try
            {
                if (localMovie.ExistingFile)
                {
                    _logger.Debug("Skipping free space check for existing movie");
                    return Decision.Accept();
                }

                var path = Directory.GetParent(localMovie.Movie.Path);
                var freeSpace = _diskProvider.GetAvailableSpace(path.FullName);

                if (!freeSpace.HasValue)
                {
                    _logger.Debug("Free space check returned an invalid result for: {0}", path);
                    return Decision.Accept();
                }

                if (freeSpace < localMovie.Size + _configService.MinimumFreeSpaceWhenImporting.Megabytes())
                {
                    _logger.Warn("Not enough free space ({0}) to import: {1} ({2})", freeSpace, localMovie, localMovie.Size);
                    return Decision.Reject("Not enough free space");
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.Error(ex, "Unable to check free disk space while importing.");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unable to check free disk space while importing: {0}", localMovie.Path);
            }

            return Decision.Accept();
        }
    }
}
