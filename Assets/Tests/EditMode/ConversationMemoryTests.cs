using System;
using NUnit.Framework;
using UnityAiNpc.Chat;
using UnityAiNpc.Npc;

namespace UnityAiNpc.Tests
{
    public class ConversationMemoryTests
    {
        [Test]
        public void BuildRequestMessages_PutsSystemPromptFirst()
        {
            var memory = new ConversationMemory("SYSTEM");
            memory.Add(ChatMessage.FromUser("hi"));
            memory.Add(ChatMessage.FromAssistant("hello"));

            var messages = memory.BuildRequestMessages();
            Assert.AreEqual(3, messages.Count);
            Assert.AreEqual(ChatMessage.System, messages[0].role);
            Assert.AreEqual("SYSTEM", messages[0].content);
            Assert.AreEqual("hello", messages[2].content);
        }

        [Test]
        public void Add_DropsOldestTurnsBeyondMessageLimit()
        {
            var memory = new ConversationMemory("S", maxTurnMessages: 4);
            for (int i = 1; i <= 4; i++)
            {
                memory.Add(ChatMessage.FromUser("u" + i));
                memory.Add(ChatMessage.FromAssistant("a" + i));
            }

            Assert.AreEqual(4, memory.Turns.Count);
            Assert.AreEqual("u3", memory.Turns[0].content);
            Assert.AreEqual("a4", memory.Turns[3].content);
        }

        [Test]
        public void Add_DropsOldestTurnsBeyondCharacterBudget()
        {
            var memory = new ConversationMemory("S", maxTurnMessages: 100, maxTurnCharacters: 25);
            memory.Add(ChatMessage.FromUser(new string('a', 10)));
            memory.Add(ChatMessage.FromAssistant(new string('b', 10)));
            memory.Add(ChatMessage.FromUser(new string('c', 10)));

            // 30 chars > 25: the first user turn goes, and the now-leading assistant turn with it.
            Assert.AreEqual(1, memory.Turns.Count);
            Assert.AreEqual(new string('c', 10), memory.Turns[0].content);
        }

        [Test]
        public void Add_KeepsNewestMessageEvenIfItAloneIsOverBudget()
        {
            var memory = new ConversationMemory("S", maxTurnMessages: 10, maxTurnCharacters: 5);
            memory.Add(ChatMessage.FromUser("this is far longer than five characters"));
            Assert.AreEqual(1, memory.Turns.Count);
        }

        [Test]
        public void History_NeverStartsWithAnAssistantTurn()
        {
            var memory = new ConversationMemory("S", maxTurnMessages: 3);
            memory.Add(ChatMessage.FromUser("u1"));
            memory.Add(ChatMessage.FromAssistant("a1"));
            memory.Add(ChatMessage.FromUser("u2"));
            memory.Add(ChatMessage.FromAssistant("a2"));

            Assert.AreEqual(ChatMessage.User, memory.Turns[0].role);
            Assert.AreEqual("u2", memory.Turns[0].content);
        }

        [Test]
        public void RemoveLast_OnlyRemovesTheExpectedMessage()
        {
            var memory = new ConversationMemory("S");
            var user = ChatMessage.FromUser("hi");
            memory.Add(user);

            Assert.IsFalse(memory.RemoveLast(ChatMessage.FromUser("something else")));
            Assert.AreEqual(1, memory.Turns.Count);
            Assert.IsTrue(memory.RemoveLast(user));
            Assert.AreEqual(0, memory.Turns.Count);
            Assert.IsFalse(memory.RemoveLast(user));
        }

        [Test]
        public void Add_RejectsSystemMessages()
        {
            var memory = new ConversationMemory("S");
            Assert.Throws<ArgumentException>(() => memory.Add(ChatMessage.FromSystem("new rules")));
        }

        [Test]
        public void Constructor_RequiresRoomForOneExchange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ConversationMemory("S", maxTurnMessages: 1));
        }
    }
}
