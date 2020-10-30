using CloudObjects.Client;
using CloudObjects.Client.Models;
using CloudObjects.Client.Static;
using DbCacheLibrary;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SqlServer.LocalDb;
using System;
using System.Threading.Tasks;

namespace Testing
{
    [TestClass]
    public class CacheTests
    {
        [TestMethod]
        public void CloudObjectsLiveSource()
        {
            var client = GetClient();

            const string objectName = "object1";
            var local = new SampleObject()
            {
                FirstName = "jinga",
                LastName = "zamooga"
            };

            client.DeleteAsync(objectName).Wait();
            client.SaveAsync(objectName, local).Wait();            

            var cache = new SampleDbCache(() => LocalDb.GetConnection("DbCacheTest"));

            // first fetch is always live
            var fetched = cache.GetAsync(objectName,
                async () =>
                {
                    var cloudObj = await client.GetAsync<SampleObject>(objectName);
                    return cloudObj.Object;
                }, TimeSpan.FromSeconds(2)).Result;

            // simulate a delay past the 2-second max age allowed
            Task.Delay(3000).Wait();

            // it's the second fetch we care about
            fetched = cache.GetAsync(objectName,
                async () =>
                {
                    var cloudObj = await client.GetAsync<SampleObject>(objectName);
                    return cloudObj.Object;
                }, TimeSpan.FromSeconds(2)).Result;

            Assert.IsTrue(fetched.FirstName.Equals(local.FirstName));
            Assert.IsTrue(fetched.LastName.Equals(local.LastName));
            Assert.IsTrue(cache.Source == ObjectSource.Live);
        }

        [TestMethod]
        public void CloudObjectsCacheSource()
        {
            var client = GetClient();

            const string objectName = "object2";
            var local = new SampleObject()
            {
                FirstName = "jinga",
                LastName = "zamooga"
            };

            client.SaveAsync(objectName, local).Wait();
            client.DeleteAsync(objectName).Wait();

            var cache = new SampleDbCache(() => LocalDb.GetConnection("DbCacheTest"));

            // first fetch will be live because it's a new object
            var fetched = cache.GetAsync(objectName,
                async () =>
                {
                    var cloudObj = await client.GetAsync<SampleObject>(objectName);
                    return cloudObj.Object;
                }, TimeSpan.FromMinutes(5)).Result;

            // second fetch will be cached because it's within 5 minute window
            fetched = cache.GetAsync(objectName, 
                async () =>
                {
                    var cloudObj = await client.GetAsync<SampleObject>(objectName);
                    return cloudObj.Object;
                }, TimeSpan.FromMinutes(5)).Result;

            Assert.IsTrue(fetched.FirstName.Equals(local.FirstName));
            Assert.IsTrue(fetched.LastName.Equals(local.LastName));
            Assert.IsTrue(cache.Source == ObjectSource.Cache);


        }

        private CloudObjectsClient GetClient()
        {
            var config = GetConfig();
            var creds = new ApiCredentials();
            config.Bind("CloudObjects", creds);
            var client = new CloudObjectsClient(HostLocations.Online, creds);
            return client;
        }

        private IConfiguration GetConfig() => new ConfigurationBuilder().AddJsonFile("Config/config.json").Build();
    }

    internal class SampleObject
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }

}