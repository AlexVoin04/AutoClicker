using AutoClicker.Enums;
using AutoClicker.Models;
using AutoClicker.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Timers;

namespace MainWindowTests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void CalculateInterval_ValidSettings_ReturnsCorrectInterval()
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.AutoClickerSettings = new AutoClickerSettings
            {
                Milliseconds = 100,
                Seconds = 1,
                Minutes = 2,
                Hours = 3
            };

            int interval = mainWindow.CalculateInterval();

            
            Assert.AreEqual(10921100, interval);
        }

        [TestMethod]
        public void IsIntervalValid_ValidInterval_ReturnsTrue()
        {
            
            var mainWindow = new MainWindow();
            mainWindow.AutoClickerSettings = new AutoClickerSettings
            {
                Milliseconds = 500,
                Seconds = 2,
                Minutes = 1,
                Hours = 0
            };

            
            bool isValid = mainWindow.IsIntervalValid();

            
            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public void IsIntervalValid_InvalidInterval_ReturnsFalse()
        {
            
            var mainWindow = new MainWindow();
            mainWindow.AutoClickerSettings = new AutoClickerSettings
            {
                Milliseconds = 0,
                Seconds = 0,
                Minutes = 0,
                Hours = 0
            };

            
            bool isValid = mainWindow.IsIntervalValid();

            
            Assert.IsFalse(isValid);
        }

        [TestMethod]
        public void CanStartOperation_ValidSettings_ReturnsTrue()
        {
            
            MainWindow mainWindow = new MainWindow();
            mainWindow.AutoClickerSettings = new AutoClickerSettings
            {
                SelectedRepeatMode = RepeatMode.Infinite,
                Milliseconds = 500,
                Seconds = 2,
                Minutes = 1,
                Hours = 0
            };
            mainWindow.clickTimer.Enabled = false;

            
            bool canStart = mainWindow.CanStartOperation();

            
            Assert.IsTrue(canStart);
        }

        [TestMethod]
        public void CanStartOperation_InvalidConditions_ReturnsFalse()
        {
            
            var mainWindow = new MainWindow();
            mainWindow.AutoClickerSettings = new AutoClickerSettings
            {
                SelectedRepeatMode = RepeatMode.Count,
                SelectedTimesToRepeat = 0,
                Milliseconds = 500,
                Seconds = 2,
                Minutes = 1,
                Hours = 0
            };
            mainWindow.clickTimer.Enabled = true;

            
            bool canStart = mainWindow.CanStartOperation();

            
            Assert.IsFalse(canStart);
        }

        [TestMethod]
        public void GetTimesToRepeat_RepeatModeCount_ReturnsTimesToRepeat()
        {
            
            var mainWindow = new MainWindow();
            mainWindow.AutoClickerSettings = new AutoClickerSettings
            {
                SelectedRepeatMode = RepeatMode.Count,
                SelectedTimesToRepeat = 5
            };

            
            int timesToRepeat = mainWindow.GetTimesToRepeat();

            
            Assert.AreEqual(5, timesToRepeat);
        }

        [TestMethod]
        public void GetTimesToRepeat_RepeatModeInfinite_ReturnsNegativeOne()
        {
            
            var mainWindow = new MainWindow();
            mainWindow.AutoClickerSettings = new AutoClickerSettings
            {
                SelectedRepeatMode = RepeatMode.Infinite
            };

            
            int timesToRepeat = mainWindow.GetTimesToRepeat();

            
            Assert.AreEqual(-1, timesToRepeat);
        }

        
    }
}
