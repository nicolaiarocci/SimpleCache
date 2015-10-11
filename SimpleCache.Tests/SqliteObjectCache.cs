﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SQLite;

// ReSharper disable once CheckNamespace
namespace Amica.vNext.SimpleCache.Tests
{
    [TestFixture]
    class SqliteObjectCache
    {
        private string _expectedDatabasePath;
        private const string AppName = "test";

        private readonly SimpleCache.SqliteObjectCache _cache = new SimpleCache.SqliteObjectCache();
        private SQLiteConnection _connection;

        [SetUp]
        public void Setup()
        {
            _cache.ApplicationName = AppName;

            _expectedDatabasePath = Path.Combine(
		Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), AppName), 
		"SimpleCache");

            Directory.CreateDirectory(_expectedDatabasePath);
            _connection = new SQLiteConnection(Path.Combine(_expectedDatabasePath, "cache.db3"));
	    _connection.DropTable<CacheElement>();

        }

	[TearDown]
        public void TearDown()
        {
            _cache.Dispose();
	    _connection.Close();
        }


        [Test]
        public void ApplicationName()
        {
            _cache.ApplicationName = null;

	    Assert.That(() => _cache.ApplicationName,
            Throws.Exception
                .TypeOf<Exception>()
                .With.Message.EqualTo("Make sure to set ApplicationName on startup"));

            _cache.ApplicationName = AppName;
            Assert.That(_cache.ApplicationName, Is.EqualTo(AppName));
        }

        [Test]
        public async Task Insert()
        {


            const string key = "key";

            Assert.That(async () => await _cache.Insert(null, "value"),
                Throws.Exception
                    .TypeOf<ArgumentNullException>()
                    .With.Property("ParamName")
                    .EqualTo(key));

            var person = new Person() {Name = "john", Age = 19};
            Assert.That(async () => await _cache.Insert(key, person), 
		Is.EqualTo(1));

            var restoredPerson = await _cache.Get<Person>(key);
            Assert.That(restoredPerson.Name, Is.EqualTo(person.Name));
            Assert.That(restoredPerson.Age, Is.EqualTo(person.Age));

            // re-inserting with same key overwrites old value.
            var anotherPerson = new Person() { Name = "mike", Age = 30 };
            Assert.That(async () => await _cache.Insert(key, anotherPerson),
                Is.EqualTo(1));

            restoredPerson = await _cache.Get<Person>(key);
            Assert.That(restoredPerson.Name, Is.EqualTo(anotherPerson.Name));
            Assert.That(restoredPerson.Age, Is.EqualTo(anotherPerson.Age));
        }

	[Test]
        public async Task Get()
        {

            const string key = "key";
            const string notExistingKey = "unkey";

            Assert.That(async () => await _cache.Get<Person>(null),
                Throws.Exception
                    .TypeOf<ArgumentNullException>()
                    .With.Property("ParamName")
                    .EqualTo(key));

            Assert.That(async () => await _cache.Get<Person>(notExistingKey),
                Throws.Exception
                    .TypeOf<KeyNotFoundException>()
                    .With.Message
                    .EqualTo(key));

            var person = new Person() {Name = "john", Age = 19};
            Assert.That(async () => await _cache.Insert(key, person), 
		Is.EqualTo(1));

            var restoredPerson = await _cache.Get<Person>(key);
            Assert.That(restoredPerson.Name, Is.EqualTo(person.Name));
            Assert.That(restoredPerson.Age, Is.EqualTo(person.Age));
        }

	[Test]
        public async Task GetCreatedAt()
        {

            const string key = "key";
            const string notExistingKey = "unkey";

            Assert.That(async () => await _cache.GetCreatedAt(null),
                Throws.Exception
                    .TypeOf<ArgumentNullException>()
                    .With.Property("ParamName")
                    .EqualTo(key));

            var person = new Person() {Name = "john", Age = 19};
            Assert.That(async () => await _cache.Insert(key, person), 
		Is.EqualTo(1));

            var createdAt = await _cache.GetCreatedAt(key);
            Assert.That(createdAt.Value.UtcDateTime, Is.EqualTo(DateTimeOffset.Now.UtcDateTime).Within(1).Seconds);

            Assert.That(async () => await _cache.GetCreatedAt(notExistingKey), 
		Is.Null);
        }

        [Test]
        public async Task GetAll()
        {
            var peopleChallenge = new List<Person>()
            {
                new Person {Name = "john", Age = 10},
                new Person {Name = "mike", Age = 20}
            };
            await _cache.Insert("john", peopleChallenge[0]); 
            await _cache.Insert("mike", peopleChallenge[1]); 

            var addressChallenge = new List<Address>()
            {
                new Address {Street = "Hollywood"},
                new Address {Street = "Junction"},
                new Address {Street = "Grand Station"},
            };
            await _cache.Insert("address1", addressChallenge[0]); 
            await _cache.Insert("address2", addressChallenge[1]); 
            await _cache.Insert("address3", addressChallenge[2]); 
	    

            var expectedCount = 2;
            var returnedPersons = await _cache.GetAll<Person>();
            var persons = returnedPersons as IList<Person> ?? returnedPersons.ToList();

            Assert.That(persons.Count(), Is.EqualTo(expectedCount));
	    for (var i = 0; i < expectedCount; i++)
	    {
	        Assert.That(persons[i].Name, Is.EqualTo(peopleChallenge[i].Name));
	        Assert.That(persons[i].Age, Is.EqualTo(peopleChallenge[i].Age));
	    }

            expectedCount = 3;
            var returnedAddresses = await _cache.GetAll<Address>();
            var addresses = returnedAddresses as IList<Address> ?? returnedAddresses.ToList();

            Assert.That(addresses.Count(), Is.EqualTo(expectedCount));
	    for (var i = 0; i < expectedCount; i++)
	    {
	        Assert.That(addresses[i].Street, Is.EqualTo(addressChallenge[i].Street));
	    }

            var returnedOthers = await _cache.GetAll<Other>();
            Assert.That(returnedOthers, Is.Empty);
        }

	[Test]
        public async Task Invalidate()
        {
            const string key = "key";
            const string notExistingKey = "unkey";

            Assert.That(async () => await _cache.Invalidate<Person>(null), 
                Throws.Exception
                    .TypeOf<ArgumentNullException>()
                    .With.Property("ParamName")
                    .EqualTo(key));

            Assert.That(async () => await _cache.Invalidate<Person>(notExistingKey), 
                Throws.Exception
                    .TypeOf<KeyNotFoundException>()
                    .With.Message
                    .EqualTo(key));

            var person = new Person() {Name = "john", Age = 19};
            Assert.That(async () => await _cache.Insert(key, person), 
		Is.EqualTo(1));

            Assert.That(async () => await _cache.Invalidate<Address>(key), 
                Throws.TypeOf<TypeMismatchException>());

	    var deleted = await _cache.Invalidate<Person>(key);
	    Assert.That(deleted, Is.EqualTo(1));

	    Assert.That(async () => await _cache.Get<Person>(key),
                Throws.Exception
                    .TypeOf<KeyNotFoundException>()
                    .With.Message
                    .EqualTo(key));
        }

	[Test]
        public async Task InvalidateAll()
        {
            var peopleChallenge = new List<Person>()
            {
                new Person {Name = "john", Age = 10},
                new Person {Name = "mike", Age = 20}
            };
            await _cache.Insert("john", peopleChallenge[0]); 
            await _cache.Insert("mike", peopleChallenge[1]); 

            var addressChallenge = new List<Address>()
            {
                new Address {Street = "Hollywood"},
                new Address {Street = "Junction"},
                new Address {Street = "Grand Station"},
            };
            await _cache.Insert("address1", addressChallenge[0]); 
            await _cache.Insert("address2", addressChallenge[1]); 
            await _cache.Insert("address3", addressChallenge[2]); 
	    

            var deleted = await _cache.InvalidateAll<Person>();
            Assert.That(deleted, Is.EqualTo(2));

            var persons = await _cache.GetAll<Person>();
            Assert.That(persons, Is.Empty);

            const int expectedCount = 3;
            var returnedAddresses = await _cache.GetAll<Address>();
            var addresses = returnedAddresses as IList<Address> ?? returnedAddresses.ToList();

            Assert.That(addresses.Count(), Is.EqualTo(expectedCount));
	    for (var i = 0; i < expectedCount; i++)
	    {
	        Assert.That(addresses[i].Street, Is.EqualTo(addressChallenge[i].Street));
	    }
        }

    }

    class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }

    class Address
    {
        public string Street { get; set; }
    }

    class Other
    {
        public string Nil { get; set; }
    }
}