
using System;
using Fractural.Utils;
using Godot;
using GDC = Godot.Collections;

namespace Fractural.RollbackNetcode
{
    [Tool]
    public class FrameDataGraphCanvas : Control
    {
        private long _start_time = 0;
        public long start_time
        {
            get => _start_time;
            set
            {
                if (_start_time != value)
                {
                    _start_time = value;
                    Update();
                    StartTimeChanged?.Invoke(value);
                }
            }
        }

        private long _cursor_time = -1;
        public long cursor_time
        {
            get => _cursor_time;
            set
            {
                if (_cursor_time != value)
                {
                    _cursor_time = value;
                    Update();
                    CursorTimeChanged?.Invoke(_cursor_time);

                    var relative_cursor_time = _cursor_time - start_time;
                    if (relative_cursor_time < 0)
                        start_time = (_cursor_time - ((int)RectSize.x - CURSOR_SCROLL_GAP));
                    else if (relative_cursor_time > RectSize.x)
                        start_time = (_cursor_time - CURSOR_SCROLL_GAP);
                }
            }
        }

        public bool show_network_arrows = true;
        //  peerIds: int[]
        public GDC.Array network_arrow_peers = new GDC.Array() { };

        public bool show_rollback_ticks = true;
        public int max_rollback_ticks = 15;

        public static readonly GDC.Dictionary FRAME_TYPE_COLOR = new GDC.Dictionary()
        {
            [Logger.FrameType.INTERFRAME] = new Color(0.3f, 0.3f, 0.3f),
            [Logger.FrameType.TICK] = new Color(0.0f, 0.75f, 0.0f),
            [Logger.FrameType.INTERPOLATION_FRAME] = new Color(0.0f, 0.0f, 0.5f),
        };
        public readonly Color ROLLBACK_LINE_COLOR = new Color(1.0f, 0.5f, 0.0f);
        public readonly Color NETWORK_ARROW_COLOR1 = new Color(1.0f, 0f, 5f, 1.0f);
        public readonly Color NETWORK_ARROW_COLOR2 = new Color(0.0f, 0.5f, 1.0f);

        public const int NETWORK_ARROW_SIZE = 8;
        public const int EXTRA_WIDTH = 1000;
        public const int PEER_GAP = 10;
        public const int CURSOR_SCROLL_GAP = 100;

        public LogData log_data;
        public DynamicFont _font;
        public DynamicFont _font_big;

        public delegate void CursorTimeChangedDelegate(long cursor_time);
        public event CursorTimeChangedDelegate CursorTimeChanged;
        public delegate void StartTimeChangedDelegate(long start_time);
        public event StartTimeChangedDelegate StartTimeChanged;

        public void Construct(LogData _log_data)
        {
            log_data = _log_data;
        }

        public void RefreshFromLogData()
        {
            if (log_data.IsLoading())
                return;
            // Remove any invalid peers from network_arrow_peers
            foreach (int peer_id in network_arrow_peers)
            {
                if (!log_data.peer_ids.Contains(peer_id))
                    network_arrow_peers.Remove(peer_id);
            }
            if (show_network_arrows)
            {
                // If we have at least two peers, set network_arrow_peers to first valid
                // options.
                if (network_arrow_peers.Count < 2 && log_data.peer_ids.Count >= 2)
                    network_arrow_peers = new GDC.Array() { log_data.peer_ids[0], log_data.peer_ids[1] };
            }
            Update();
        }

        public override void _Ready()
        {
            _font = new DynamicFont();
            _font.FontData = GD.Load<DynamicFontData>("res://addons/godot-rollback-netcode/log_inspector/monogram_extended.ttf");
            _font.Size = 16;

            _font_big = new DynamicFont();
            _font_big.FontData = GD.Load<DynamicFontData>("res://addons/godot-rollback-netcode/log_inspector/monogram_extended.ttf");
            _font_big.Size = 32;
        }

        public override void _GuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButtonEvent)
            {
                if (mouseButtonEvent.ButtonIndex == (int)ButtonList.Left && mouseButtonEvent.Pressed)
                    cursor_time = ((int)(start_time + mouseButtonEvent.Position.x));
            }
        }

        public void _DrawPeer(int peer_id, Rect2 peer_rect, GDC.Dictionary draw_data)
        {
            //GD.Print("DrawPeer");
            //GD.Print("1");
            var relative_start_time = start_time - EXTRA_WIDTH;
            if (relative_start_time < 0)
                relative_start_time = 0;

            //GD.Print("2");
            long absolute_start_time = log_data.start_time + relative_start_time;
            long absolute_end_time = absolute_start_time + (long)peer_rect.Size.x + (EXTRA_WIDTH * 2);
            LogData.FrameData frame = log_data.GetFrameByTime(peer_id, absolute_start_time);
            if (frame == null && log_data.GetFrameCount(peer_id) > 0)
                frame = log_data.GetFrame(peer_id, 0);
            if (frame == null)
                return;
            //GD.Print("3");
            // data: GDC.Array<GDC.Array: [font: DynamicFont, position: Vector2, tick_letter: string, color: Color]>
            GDC.Array tick_numbers_to_draw = new GDC.Array() { };

            bool capture_network_arrow_positions = show_network_arrows && network_arrow_peers.Contains(peer_id);
            GDC.Dictionary network_arrow_start_positions = new GDC.Dictionary() { };
            GDC.Dictionary network_arrow_end_positions = new GDC.Dictionary() { };

            //GD.Print("4");
            int other_network_arrow_peer_id = 0;
            if (capture_network_arrow_positions)
            {
                foreach (int other_peer_id in network_arrow_peers)
                {
                    if (other_peer_id != peer_id)
                    {
                        other_network_arrow_peer_id = other_peer_id;
                        break;
                    }
                }
            }
            //GD.Print("5");
            string other_network_arrow_peer_key = $"remote_ticks_received_from_{other_network_arrow_peer_id}";

            // Adjust the peer rect for the extra width.
            var extended_peer_rect = peer_rect;
            extended_peer_rect.Position = extended_peer_rect.Position - new Vector2((start_time > EXTRA_WIDTH ? EXTRA_WIDTH : start_time), 0);
            extended_peer_rect.Size = extended_peer_rect.Size + new Vector2(EXTRA_WIDTH * 2, 0);

            //GD.Print("6");

            Vector2 last_rollback_point = default;
            while (frame.start_time <= absolute_end_time)
            {
                //GD.Print("6.1");
                Rect2 frame_rect = new Rect2(
                    new Vector2(extended_peer_rect.Position.x + frame.start_time - absolute_start_time, extended_peer_rect.Position.y),
                    new Vector2(frame.end_time - frame.start_time, extended_peer_rect.Size.y));

                //GD.Print("6.2");
                if (frame_rect.Intersects(extended_peer_rect))
                {
                    //GD.Print("6.2.1");
                    frame_rect = frame_rect.Clip(extended_peer_rect);
                    if (frame_rect.Size.x == 0)
                        frame_rect.Size = new Vector2(1, frame_rect.Size.y);
                    //GD.Print("6.2.2");
                    bool skipped = frame.data.Get<bool>("skipped", false);
                    bool fatal_error = frame.data.Get<bool>("fatal_error", false);
                    //GD.Print("6.2.3");
                    Vector2 center_position = frame_rect.Position + (frame_rect.Size / 2f);
                    Color frame_color = default;
                    if (fatal_error)
                        frame_color = new Color(1f, 0f, 0f);
                    else if (skipped)
                        frame_color = new Color(1f, 1f, 0f);
                    if (frame_rect.Size.x <= 1f)
                    {
                        frame_rect.Size = new Vector2(3f, frame_rect.Size.y);
                        frame_rect.Position = frame_rect.Position - new Vector2(1.5f, 0);
                    }

                    //GD.Print("6.2.4");
                    if (frame.data.Contains("skip_reason"))
                    {
                        string tick_letter = "";

                        switch (frame.data.Get<Logger.SkipReason>("skip_reason"))
                        {
                            case Logger.SkipReason.INPUT_BUFFER_UNDERRUN:
                                tick_letter = "B";
                                break;
                            case Logger.SkipReason.WAITING_TO_REGAIN_SYNC:
                                tick_letter = "W";
                                break;
                            case Logger.SkipReason.ADVANTAGE_ADJUSTMENT:
                                tick_letter = "A";
                                break;
                        }
                        if (tick_letter != "")
                            tick_numbers_to_draw.Add(new GDC.Array() { _font_big, center_position - new Vector2(5, 0), tick_letter, new Color("f04dff") });
                    }
                    else
                        frame_color = FRAME_TYPE_COLOR.Get<Color>(frame.type);
                    DrawRect(frame_rect, frame_color);

                    //GD.Print("6.2.5");
                    if (frame.type == Logger.FrameType.TICK && frame.data.Contains("tick") && !skipped)
                    {
                        int tick = frame.data.Get<int>("tick");
                        tick_numbers_to_draw.Add(new GDC.Array() { _font, center_position - new Vector2(3, 0), GD.Str(tick), new Color(1f, 1f, 1f) });
                        if (frame.data.Contains("input_tick") && capture_network_arrow_positions)
                        {
                            int input_tick = frame.data.Get<int>("input_tick");
                            network_arrow_start_positions[input_tick] = center_position;
                        }
                    }

                    //GD.Print("6.2.6");
                    if (capture_network_arrow_positions && frame.data.Contains(other_network_arrow_peer_key))
                    {
                        foreach (int tick in frame.data.Get<GDC.Dictionary>(other_network_arrow_peer_key).Keys)
                            network_arrow_end_positions[tick] = center_position;
                    }

                    //GD.Print("6.2.7");

                    if (show_rollback_ticks && frame.data.Contains("rollback_ticks"))
                    {
                        //GD.Print("6.2.7.1 frame data: ", JSON.Print(frame.data));
                        var rollback_height = extended_peer_rect.Size.y * ((float)(frame.data.Get<int>("rollback_ticks")) / (float)(max_rollback_ticks));
                        //GD.Print("6.2.7.2");
                        if (rollback_height > extended_peer_rect.Size.y)
                            rollback_height = extended_peer_rect.Size.y;
                        //GD.Print("6.2.7.3");
                        Vector2 rollback_point = new Vector2(center_position.x, frame_rect.Position.y + frame_rect.Size.y - rollback_height);
                        //GD.Print("6.2.7.4");
                        if (last_rollback_point != null)
                            DrawLine(last_rollback_point, rollback_point, ROLLBACK_LINE_COLOR, 2f, true);
                        //GD.Print("6.2.7.5");
                        last_rollback_point = rollback_point;
                    }
                }

                //GD.Print("6.3");
                // Move on to the next frame.
                if (frame.frame < log_data.GetFrameCount(peer_id) - 1)
                    frame = log_data.GetFrame(peer_id, frame.frame + 1);
                else
                    break;
            }

            //GD.Print("7");
            foreach (GDC.Array tick_number_to_draw in tick_numbers_to_draw)
                DrawString(tick_number_to_draw.ElementAt<Font>(0), tick_number_to_draw.ElementAt<Vector2>(1), tick_number_to_draw.ElementAt<string>(2), tick_number_to_draw.ElementAt<Color>(3));


            //GD.Print("8");
            if (capture_network_arrow_positions)
            {
                if (!draw_data.Contains("network_arrow_positions"))
                    draw_data["network_arrow_positions"] = new GDC.Array() { };
                draw_data.Get<GDC.Array>("network_arrow_positions").Add(new GDC.Array() { network_arrow_start_positions, network_arrow_end_positions });
            }

            //GD.Print("9");
        }

        public void _DrawNetworkArrows(GDC.Dictionary start_positions, GDC.Dictionary end_positions, Color color)
        {
            foreach (var tick in start_positions.Keys)
            {
                if (!end_positions.Contains(tick))
                    continue;
                var start_position = start_positions.Get<Vector2>(tick);
                var end_position = end_positions.Get<Vector2>(tick);

                if (start_position.y < end_position.y)
                {
                    start_position.y += 10;
                    end_position.y -= 15;
                }
                else
                {
                    start_position.y -= 15;
                    end_position.y += 10;
                }
                DrawLine(start_position, end_position, color, 2f, true);

                // Draw the arrow head.
                var sqrt12 = Mathf.Sqrt(0.5f);
                Vector2 vector = end_position - start_position;
                Transform2D t = new Transform2D(vector.Angle(), end_position);
                var points = new Vector2[] {
                    t * new Vector2(0, 0),
                    t * new Vector2(-NETWORK_ARROW_SIZE, sqrt12 * NETWORK_ARROW_SIZE),
                    t * new Vector2(-NETWORK_ARROW_SIZE, sqrt12 * -NETWORK_ARROW_SIZE),
                };
                var colors = new Color[]{
                    color,
                    color,
                    color,
                };
                DrawPrimitive(points, colors, new Vector2[] { });
            }
        }

        public override void _Draw()
        {
            if (log_data == null || log_data.IsLoading())
                return;

            var peer_count = log_data.peer_ids.Count;
            if (peer_count == 0)
                return;

            // 
            //  {
            //      network_arrow_positions: {
            //          network_arrow_start_positions: Vector2[], 
            //          network_arrow_end_positions: Vector2[],
            //      }[],
            //  }
            //
            GDC.Dictionary draw_data = new GDC.Dictionary() { };
            // [peerId: int]: rect: Rect2
            GDC.Dictionary peer_rects = new GDC.Dictionary() { };

            float peer_height = (RectSize.y - ((peer_count - 1) * PEER_GAP)) / peer_count;
            float current_y = 0;
            for (int peer_index = 0; peer_index < peer_count; peer_index++)
            {
                var peer_id = log_data.peer_ids.ElementAt<int>(peer_index);
                Rect2 peer_rect = new Rect2(
                    new Vector2(0, current_y),
                    new Vector2(RectSize.x, peer_height));
                peer_rects[peer_id] = peer_rect;
                _DrawPeer(peer_id, peer_rect, draw_data);
                current_y += peer_height + PEER_GAP;
            }
            if (show_network_arrows)
            {
                GDC.Array network_arrows_positions = draw_data.Get("network_arrow_positions", new GDC.Array() { });
                if (network_arrows_positions.Count == 2)
                {
                    _DrawNetworkArrows(network_arrows_positions.ElementAt<GDC.Array>(0).ElementAt<GDC.Dictionary>(0), network_arrows_positions.ElementAt<GDC.Array>(1).ElementAt<GDC.Dictionary>(1), NETWORK_ARROW_COLOR1);
                    _DrawNetworkArrows(network_arrows_positions.ElementAt<GDC.Array>(1).ElementAt<GDC.Dictionary>(0), network_arrows_positions.ElementAt<GDC.Array>(0).ElementAt<GDC.Dictionary>(1), NETWORK_ARROW_COLOR2);
                }
            }
            foreach (int peer_id in peer_rects.Keys)
            {
                Rect2 peer_rect = peer_rects.Get<Rect2>(peer_id);
                DrawString(_font, peer_rect.Position + new Vector2(0, PEER_GAP), $"Peer {peer_id}", new Color(1f, 1f, 1f));
            }
            if (cursor_time >= start_time && cursor_time <= start_time + RectSize.x)
            {
                DrawLine(
                    new Vector2(cursor_time - start_time, 0),
                    new Vector2(cursor_time - start_time, RectSize.y),
                    new Color(1f, 0f, 0f),
                    3f);
            }
        }
    }
}