using System;

namespace Weikio.TypeGenerator.Tests
{
    public class TestClass
    {
        public int Counter { get; set; }

        public void Run()
        {
            Console.WriteLine("Hello world");
        }

        public string HelloWorld(int i, bool test)
        {
            if (test)
            {
                return i.ToString();
            }

            return "Hello " + i;
        }

        public string SometimesHidden(string y)
        {
            return y;
        }

        public void AddCount()
        {
            Counter += 1;
        }

        public void AddCount(int i)
        {
            Counter += i;
        }

        public int GetCount()
        {
            return Counter;
        }
    }

    public class CustomBaseClass
    {
        public string DoWork()
        {
            return "Hello from base class";
        }
    }

    public interface ITestInterface1
    {
    }

    public interface ITestInterface2
    {
    }

    public class Product
    {
        public string Name
        {
            get;
            set;
        }

        public int Id { get; set; }
        public string Description { get; set; }
    }

    public class ProductWithGetOnlyProperty
    {
        public string Name
        {
            get;
        }

        public int Id { get; set; }
        public string Description { get; set; }
    }

    public class ProductWithPrivateProperty
    {
        private string Name { get; set; }

        public int Id { get; set; }
        public string Description { get; set; }
    }

    public abstract class Entity
    {
        public Guid Id { get; set; }
    }

    public class EntityImpl : Entity
    {
        public string Name { get; set; }
    }
}
