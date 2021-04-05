using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Tests
{
    public class IntegrationTests : InputTestFixture
    {
        private GameController _game;
        private Gamepad _gamepad;
        private Keyboard _keyboard;
        
        [SetUp]
        public void Setup()
        {
            _gamepad = InputSystem.AddDevice<Gamepad>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            
            _game = Object.Instantiate(Resources.Load<GameObject>("Prefabs/GameController")).GetComponent<GameController>();
            
            PlayerController playerPrefab = Resources.Load<PlayerController>("Prefabs/Player");
            _game.player = Object.Instantiate(playerPrefab).GetComponent<PlayerController>();
            _game.player.PlayerInput.enabled = true;
            
            TimeMachineController timeMachine = Object.Instantiate(Resources.Load<GameObject>("Prefabs/TimeMachine")).GetComponent<TimeMachineController>();
            BasicTimeTracker moveableBox = Object.Instantiate(Resources.Load<GameObject>("Prefabs/MoveableBox")).GetComponent<BasicTimeTracker>();
            
            _game.levelEndObject = Object.Instantiate(Resources.Load<GameObject>("Prefabs/LevelEnd")).GetComponent<BoxCollider2D>();
            _game.levelEndObject.transform.position = new Vector3(30, 0, 0);
        }

        [TearDown]
        public void Teardown()
        {
            Object.Destroy(_game.gameObject);
        }
        
        [UnityTest]
        public IEnumerator IntegrationTestsWithEnumeratorPasses()
        {
            TimeMachineController timeMachine = _game.timeMachines[0];
            
            Assert.IsFalse(timeMachine.IsActivatedOrOccupied);
            Assert.AreEqual(-1, timeMachine.Countdown.Current);
            Assert.IsFalse(_game.player.IsActivating);
            yield return null;

            PressAndRelease(_keyboard.spaceKey);
            yield return null;
            //TODO: create some proper integration tests
            //Assert.IsTrue(_game.player.IsActivating);

            //Assert.AreEqual(0, timeMachine.Countdown.Current);

            // Use the Assert class to test conditions.
            // yield to skip a frame
            for(int i = 0; i < 100; i++) yield return null;
        }
    }
}