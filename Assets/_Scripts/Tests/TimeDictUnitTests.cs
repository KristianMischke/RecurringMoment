using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests
{
    public class TimeDictUnitTests
    {
        [Test]
        public void VariableTimelineBoolTest()
        {
            VariableTimeline<bool> timeline = new VariableTimeline<bool>();
            
            Assert.IsFalse(timeline.Get(0)); // default is false
            
            timeline.Set(10, true);
            Assert.IsFalse(timeline.Get(0)); // before point still false
            Assert.IsTrue(timeline.Get(10)); // at point is true
            Assert.IsTrue(timeline.Get(20)); // after point is true
            
            timeline.Set(25, false);
            Assert.IsTrue(timeline.Get(15));
            Assert.IsFalse(timeline.Get(25));
            Assert.IsFalse(timeline.Get(30));
        }
        
        [Test]
        public void VariableTimelineBoolNoForceTest()
        {
            VariableTimeline<bool> timeline = new VariableTimeline<bool>();
            
            timeline.Set(0, true);
            timeline.Set(25, false);
            Assert.IsTrue(timeline.Get(15));
            Assert.IsFalse(timeline.Get(25));
            Assert.IsFalse(timeline.Get(30));
            
            timeline.Set(30, false, force:false); // NO FORCE set future value to same as the propagated value from time=25
            Assert.IsFalse(timeline.Get(30));
            timeline.Set(27,  true); // now set value between two 'false' data points
            Assert.IsFalse(timeline.Get(26));
            Assert.IsTrue(timeline.Get(27));
            Assert.IsTrue(timeline.Get(28));
            Assert.IsTrue(timeline.Get(30)); // values should NOT be affected by the 30 data point b/c it was not forced
            Assert.IsTrue(timeline.Get(31));
        }
        
        [Test]
        public void VariableTimelineBoolWithForceTest()
        {
            VariableTimeline<bool> timeline = new VariableTimeline<bool>();
            
            timeline.Set(0, true);
            timeline.Set(25, false);
            Assert.IsTrue(timeline.Get(15));
            Assert.IsFalse(timeline.Get(25));
            Assert.IsFalse(timeline.Get(30));
            
            timeline.Set(30, false, force:true); // FORCE set future value to same as the propagated value from time=25
            Assert.IsFalse(timeline.Get(30));
            timeline.Set(27,  true); // now set value between two 'false' data points
            Assert.IsFalse(timeline.Get(26));
            Assert.IsTrue(timeline.Get(27));
            Assert.IsTrue(timeline.Get(28));
            Assert.IsFalse(timeline.Get(30)); // values should be affected by the 30 data point b/c it was forced
            Assert.IsFalse(timeline.Get(31));
        }
        
        
        [Test]
        public void VariableTimelineIntTest()
        {
            VariableTimeline<int> timeline = new VariableTimeline<int>();
            
            Assert.AreEqual(0, timeline.Get(0)); // default is 0
            
            timeline.Set(10, 55);
            Assert.AreEqual(0, timeline.Get(0)); // before point still 0
            Assert.AreEqual(55, timeline.Get(10)); // at point is 55
            Assert.AreEqual(55, timeline.Get(20)); // after point is 55
            
            timeline.Set(25, 1111);
            Assert.AreEqual(55, timeline.Get(15));
            Assert.AreEqual(1111, timeline.Get(25));
            Assert.AreEqual(1111, timeline.Get(30));
        }
        
        [Test]
        public void TimeDictTest()
        {
            TimeDict history = new TimeDict();
            
            Assert.AreEqual(false, history.Get<bool>(0, "test")); // no value should return template type default

            history.Set(10, "int_var", 55);
            Assert.AreEqual(0, history.Get<int>(0, "int_var")); // before point still 0
            Assert.AreEqual(55, history.Get<int>(10, "int_var")); // at point is 55
            Assert.AreEqual(55, history.Get<int>(20, "int_var")); // after point is 55
            
            Assert.AreEqual(0, history[0].Get<int>("int_var"));

            history.Set(25, "int_var", 1111);
            Assert.AreEqual(55, history.Get<int>(15, "int_var"));
            Assert.AreEqual(1111, history.Get<int>(25, "int_var"));
            Assert.AreEqual(1111, history.Get<int>(30, "int_var"));
        }
    }
}
