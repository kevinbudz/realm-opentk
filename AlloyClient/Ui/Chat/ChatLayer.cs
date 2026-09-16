using System.Collections.Generic;
using Alloy.UiLib.Core;
using Alloy.Engine;
using AlloyClient.Game;

namespace AlloyClient.Ui.Chat;

public class ChatLayer : Sprite {

    private readonly static Queue<SpeechData> Queue = new();

    private readonly Dictionary<int, SpeechBubble> _bubbles = [];
    private readonly List<int> _expiredBubbleIds = [];
    private readonly List<SpeechBubble> _detachedBubbles = [];

    public ChatLayer() {
        AddEventListener(Event.RemovedFromStage, OnRemovedFromStage);
    }

    public static void QueueSpeech(SpeechData data) => Queue.Enqueue(data);

    public void Update(in GameTime gameTime, in Camera camera) {
        RemoveDetachedBubbles();

        while (Queue.TryDequeue(out var data)) {
            if (_bubbles.Remove(data.Owner.ObjectId, out var previous)) {
                RemoveBubble(previous);
            }

            var sprite = new SpeechBubble(data, gameTime.TotalMs);
            _bubbles[data.Owner.ObjectId] = sprite;
            AddChild(sprite);
        }

        _expiredBubbleIds.Clear();
        foreach (var (key, bubble) in _bubbles) {
            if (!bubble.Update(in gameTime, in camera)) {
                _expiredBubbleIds.Add(key);
            }
        }

        foreach (var key in _expiredBubbleIds) {
            if (_bubbles.Remove(key, out var bubble)) {
                RemoveBubble(bubble);
            }
        }
    }

    private void RemoveDetachedBubbles() {
        foreach (var bubble in _detachedBubbles) {
            RemoveChild(bubble);
        }

        _detachedBubbles.Clear();
    }

    private void RemoveBubble(SpeechBubble bubble) {
        if (bubble.Parent == this) {
            RemoveChild(bubble);
        }
    }

    private void OnRemovedFromStage() {
        // Speech from the previous game screen must not be replayed by a new one.
        Queue.Clear();

        foreach (var bubble in _bubbles.Values) {
            _detachedBubbles.Add(bubble);
        }

        _bubbles.Clear();
    }
}
