using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.DecisionEngine.Specifications.RssSync;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests.RssSync
{
    [TestFixture]
    public class DelaySpecificationFixture : CoreTest<DelaySpecification>
    {
        private QualityProfile _profile;
        private DelayProfile _delayProfile;
        private RemoteMovie _remoteMovie;

        [SetUp]
        public void Setup()
        {
            _profile = Builder<QualityProfile>.CreateNew()
                                       .Build();

            _delayProfile = Builder<DelayProfile>.CreateNew()
                                                 .With(d => d.PreferredProtocol = DownloadProtocol.Usenet)
                                                 .Build();

            var movie = Builder<Movie>.CreateNew()
                                        .With(s => s.QualityProfile = _profile)
                                        .Build();

            var movieFile = Builder<MovieFile>.CreateNew().With(f => f.MovieId == movie.Id).Build();

            movie.MovieFiles = new List<MovieFile> { movieFile };

            _remoteMovie = Builder<RemoteMovie>.CreateNew()
                                                   .With(r => r.Movie = movie)
                                                   .Build();

            _profile.Items = new List<QualityProfileQualityItem>();
            _profile.Items.Add(new QualityProfileQualityItem { Allowed = true, Quality = Quality.HDTV720p });
            _profile.Items.Add(new QualityProfileQualityItem { Allowed = true, Quality = Quality.WEBDL720p });
            _profile.Items.Add(new QualityProfileQualityItem { Allowed = true, Quality = Quality.Bluray720p });

            _profile.Cutoff = Quality.WEBDL720p.Id;

            _remoteMovie.ParsedMovieInfo = new ParsedMovieInfo();
            _remoteMovie.Release = new ReleaseInfo();
            _remoteMovie.Release.DownloadProtocol = DownloadProtocol.Usenet;

            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.BestForTags(It.IsAny<HashSet<int>>()))
                  .Returns(_delayProfile);

            Mocker.GetMock<IPendingReleaseService>()
                  .Setup(s => s.GetPendingRemoteMovies(It.IsAny<int>()))
                  .Returns(new List<RemoteMovie>());
        }

        private void GivenExistingFile(QualityModel quality)
        {
            // _remoteEpisode.Episodes.First().EpisodeFileId = 1;
            var movieFile = new MovieFile { Quality = quality };

            _remoteMovie.Movie.MovieFiles = new List<MovieFile> { movieFile };
        }

        private void GivenUpgradeForExistingFile()
        {
            Mocker.GetMock<IUpgradableSpecification>()
                  .Setup(s => s.IsUpgradable(It.IsAny<QualityProfile>(), It.IsAny<QualityModel>(), It.IsAny<List<CustomFormat>>(), It.IsAny<QualityModel>(), It.IsAny<List<CustomFormat>>()))
                  .Returns(true);
        }

        [Test]
        public void should_be_true_when_user_invoked_search()
        {
            Subject.IsSatisfiedBy(new RemoteMovie(), new MovieSearchCriteria() { UserInvokedSearch = true }).Should().OnlyContain(x => x.Accepted);
        }

        [Test]
        public void should_be_false_when_system_invoked_search_and_release_is_younger_than_delay()
        {
            _remoteMovie.ParsedMovieInfo.Quality = new QualityModel(Quality.SDTV);
            _remoteMovie.Release.PublishDate = DateTime.UtcNow;

            _delayProfile.UsenetDelay = 720;

            Subject.IsSatisfiedBy(_remoteMovie, new MovieSearchCriteria()).Should().OnlyContain(x => !x.Accepted);
        }

        [Test]
        public void should_be_true_when_profile_does_not_have_a_delay()
        {
            _delayProfile.UsenetDelay = 0;

            Subject.IsSatisfiedBy(_remoteMovie, null).Should().OnlyContain(x => x.Accepted);
        }

        [Test]
        public void should_be_false_when_quality_is_last_allowed_in_profile_and_bypass_disabled()
        {
            _remoteMovie.Release.PublishDate = DateTime.UtcNow;
            _remoteMovie.ParsedMovieInfo.Quality = new QualityModel(Quality.Bluray720p);

            _delayProfile.UsenetDelay = 720;

            Subject.IsSatisfiedBy(_remoteMovie, null).Should().OnlyContain(x => !x.Accepted);
        }

        [Test]
        public void should_be_true_when_quality_is_last_allowed_in_profile_and_bypass_enabled()
        {
            _delayProfile.UsenetDelay = 720;
            _delayProfile.BypassIfHighestQuality = true;

            _remoteMovie.Release.PublishDate = DateTime.UtcNow;
            _remoteMovie.ParsedMovieInfo.Quality = new QualityModel(Quality.Bluray720p);

            Subject.IsSatisfiedBy(_remoteMovie, null).Should().OnlyContain(x => x.Accepted);
        }

        [Test]
        public void should_be_true_when_release_is_older_than_delay()
        {
            _remoteMovie.ParsedMovieInfo.Quality = new QualityModel(Quality.HDTV720p);
            _remoteMovie.Release.PublishDate = DateTime.UtcNow.AddHours(-10);

            _delayProfile.UsenetDelay = 60;

            Subject.IsSatisfiedBy(_remoteMovie, null).Should().OnlyContain(x => x.Accepted);
        }

        [Test]
        public void should_be_false_when_release_is_younger_than_delay()
        {
            _remoteMovie.ParsedMovieInfo.Quality = new QualityModel(Quality.SDTV);
            _remoteMovie.Release.PublishDate = DateTime.UtcNow;

            _delayProfile.UsenetDelay = 720;

            Subject.IsSatisfiedBy(_remoteMovie, null).Should().OnlyContain(x => !x.Accepted);
        }

        [Test]
        public void should_be_true_when_release_is_a_proper_for_existing_movie()
        {
            _remoteMovie.ParsedMovieInfo.Quality = new QualityModel(Quality.HDTV720p, new Revision(version: 2));
            _remoteMovie.Release.PublishDate = DateTime.UtcNow;

            GivenExistingFile(new QualityModel(Quality.HDTV720p));
            GivenUpgradeForExistingFile();

            Mocker.GetMock<IUpgradableSpecification>()
                  .Setup(s => s.IsRevisionUpgrade(It.IsAny<QualityModel>(), It.IsAny<QualityModel>()))
                  .Returns(true);

            _delayProfile.UsenetDelay = 720;

            Subject.IsSatisfiedBy(_remoteMovie, null).Should().OnlyContain(x => x.Accepted);
        }

        [Test]
        public void should_be_true_when_release_is_a_real_for_existing_movie()
        {
            _remoteMovie.ParsedMovieInfo.Quality = new QualityModel(Quality.HDTV720p, new Revision(real: 1));
            _remoteMovie.Release.PublishDate = DateTime.UtcNow;

            GivenExistingFile(new QualityModel(Quality.HDTV720p));
            GivenUpgradeForExistingFile();

            Mocker.GetMock<IUpgradableSpecification>()
                  .Setup(s => s.IsRevisionUpgrade(It.IsAny<QualityModel>(), It.IsAny<QualityModel>()))
                  .Returns(true);

            _delayProfile.UsenetDelay = 720;

            Subject.IsSatisfiedBy(_remoteMovie, null).Should().OnlyContain(x => x.Accepted);
        }

        [Test]
        public void should_be_false_when_release_is_proper_for_existing_movie_of_different_quality()
        {
            _remoteMovie.ParsedMovieInfo.Quality = new QualityModel(Quality.HDTV720p, new Revision(version: 2));
            _remoteMovie.Release.PublishDate = DateTime.UtcNow;

            GivenExistingFile(new QualityModel(Quality.SDTV));

            _delayProfile.UsenetDelay = 720;

            Subject.IsSatisfiedBy(_remoteMovie, null).Should().OnlyContain(x => !x.Accepted);
        }

        [Test]
        public void should_be_false_when_custom_format_score_is_above_minimum_but_bypass_disabled()
        {
            _remoteMovie.Release.PublishDate = DateTime.UtcNow;
            _remoteMovie.CustomFormatScore = 100;

            _delayProfile.UsenetDelay = 720;
            _delayProfile.MinimumCustomFormatScore = 50;

            Subject.IsSatisfiedBy(_remoteMovie, null).All(x => x.Accepted).Should().BeFalse();
        }

        [Test]
        public void should_be_false_when_custom_format_score_is_above_minimum_and_bypass_enabled_but_under_minimum()
        {
            _remoteMovie.Release.PublishDate = DateTime.UtcNow;
            _remoteMovie.CustomFormatScore = 5;

            _delayProfile.UsenetDelay = 720;
            _delayProfile.BypassIfAboveCustomFormatScore = true;
            _delayProfile.MinimumCustomFormatScore = 50;

            Subject.IsSatisfiedBy(_remoteMovie, null).All(x => x.Accepted).Should().BeFalse();
        }

        [Test]
        public void should_be_true_when_custom_format_score_is_above_minimum_and_bypass_enabled()
        {
            _remoteMovie.Release.PublishDate = DateTime.UtcNow;
            _remoteMovie.CustomFormatScore = 100;

            _delayProfile.UsenetDelay = 720;
            _delayProfile.BypassIfAboveCustomFormatScore = true;
            _delayProfile.MinimumCustomFormatScore = 50;

            Subject.IsSatisfiedBy(_remoteMovie, null).All(x => x.Accepted).Should().BeTrue();
        }
    }
}
