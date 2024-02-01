using System.Collections.Generic;
using FakeItEasy;
using FluentAssertions;
using NUnit.Framework;

namespace MockFramework
{
    public class ThingCache
    {
        private readonly IDictionary<string, Thing> dictionary = new Dictionary<string, Thing>();

        private readonly IThingService thingService;

        public ThingCache(IThingService thingService)
        {
            this.thingService = thingService;
        }

        public Thing Get(string thingId)
        {
            Thing thing;
            if (dictionary.TryGetValue(thingId, out thing))
                return thing;
            if (thingService.TryRead(thingId, out thing))
            {
                dictionary[thingId] = thing;
                return thing;
            }

            return null;
        }
    }

    [TestFixture]
    public class ThingCache_Should
    {
        private IThingService thingService;
        private ThingCache thingCache;

        private const string thingId1 = "TheDress";
        private Thing thing1 = new Thing(thingId1);

        private const string thingId2 = "CoolBoots";
        private Thing thing2 = new Thing(thingId2);
        
        [SetUp]
        public void SetUp()
        {
            thingService = A.Fake<IThingService>();
            A.CallTo(() => thingService.TryRead(thingId1, out thing1))
                .Returns(true)
                .AssignsOutAndRefParameters(thing1);
            A.CallTo(() => thingService.TryRead(thingId2, out thing2))
                .Returns(true)
                .AssignsOutAndRefParameters(thing2);
            thingCache = new ThingCache(thingService);
        }
        
        [Test]
        public void CallsIThingServiceWhenNotCached()
        {
            thingCache.Get(thingId1).Should().BeEquivalentTo(thing1);
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void CallsIThingServiceOnceWhenCached()
        {
            thingCache.Get(thingId1).Should().BeEquivalentTo(thing1);
            thingCache.Get(thingId1).Should().BeEquivalentTo(thing1);
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void CallsIThingServiceOnceWhenCachedSeveralThings()
        {
            for (var i = 0; i < 2; i++)
            {
                thingCache.Get(thingId1).Should().BeEquivalentTo(thing1);
                thingCache.Get(thingId2).Should().BeEquivalentTo(thing2);
            }

            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).MustHaveHappenedOnceExactly();
            A.CallTo(() => thingService.TryRead(thingId2, out thing2)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void ShouldReturnBullWhenNotFoundInService()
        {
            A.CallTo(() => thingService.TryRead(thingId1, out thing1))
                .Returns(false);
            thingCache.Get(thingId1).Should().Be(null);
            A.CallTo(() => thingService.TryRead(thingId1, out thing1)).MustHaveHappenedOnceExactly();
        }

        [Test]
        public void ShouldCallAgainWhenNotFoundInService()
        {
            A.CallTo(() => thingService.TryRead(thingId1, out thing1))
                .Returns(false);
            var failedTimes = 2;
            for (var i = 0; i < failedTimes; i++)
                thingCache.Get(thingId1).Should().Be(null);
            A.CallTo(() => thingService.TryRead(thingId1, out thing1))
                .Returns(true)
                .AssignsOutAndRefParameters(thing1);
            thingCache.Get(thingId1).Should().BeEquivalentTo(thing1);
            A.CallTo(() => thingService.TryRead(thingId1, out thing1))
                .MustHaveHappened(failedTimes + 1, Times.Exactly);
        }
    }
}
