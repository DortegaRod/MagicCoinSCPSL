using CommandSystem;
using Exiled.API.Features;
using Exiled.Permissions.Extensions;
using System;
using UnityEngine;

namespace ItemRandomizerPlugin_SCPSL.RoomPoints {
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class RoomPoint : ICommand {
        private const string RequiredPermission = "irndpl.roompoint";
        private const float MaxDistance = 100f;

        public string Command { get; } = "roompoint";

        public string[] Aliases { get; } = { "rp" };

        public string Description { get; } = "Prints the point you are looking at, in coordinates local to its room, ready to paste into the plugin config.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response) {
            if (!sender.CheckPermission(RequiredPermission)) {
                response = $"You need the '{RequiredPermission}' permission to use this command.";
                return false;
            }

            Player player = Player.Get(sender);
            if (player == null) {
                response = "This command has to be run in-game - the server console has no camera.";
                return false;
            }

            Transform camera = player.CameraTransform.transform;
            if (!Physics.Raycast(camera.position, camera.forward, out RaycastHit hit, MaxDistance)) {
                response = $"Nothing within {MaxDistance}m of your crosshair.";
                return false;
            }

            Room room = Room.Get(hit.point);
            if (room == null) {
                response = "That point does not belong to any room.";
                return false;
            }

            // The raycast hit is already in world space, so it has to be run through the INVERSE of
            // the room transform to become a local coordinate. TransformPoint does the opposite and
            // produces a point that no longer refers to anything. Nudge it up off the floor first.
            Vector3 local = room.Transform.InverseTransformPoint(hit.point + (Vector3.up * 0.1f));

            response = "\nRoomPoint for the position you are looking at:" +
                       $"\n  RoomType: {room.Type}" +
                       "\n\nPaste into the plugin config:" +
                       $"\n  randomizer_room: {room.Type}" +
                       "\n  randomizer_drop_point:" +
                       $"\n    x: {local.x}" +
                       $"\n    y: {local.y}" +
                       $"\n    z: {local.z}";

            return true;
        }
    }
}
