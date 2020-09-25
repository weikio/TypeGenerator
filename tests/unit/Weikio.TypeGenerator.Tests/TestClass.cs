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
}
