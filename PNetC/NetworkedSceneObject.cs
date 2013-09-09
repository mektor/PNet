﻿using System;
using System.Collections.Generic;
using System.Text;
using PNet;
using Lidgren.Network;

namespace PNetC
{
    /// <summary>
    /// Objects that exist in a scene with pre-synchronized network id's
    /// </summary>
    public class NetworkedSceneObject
    {
        int _networkID;
        /// <summary>
        /// The scene/room Network ID of this item. Should be unique per object
        /// </summary>
        public int NetworkID
        {
            get
            {
                return _networkID;
            }
        }

        public NetworkedSceneObject(int _networkID)
        {
            sceneObjects[_networkID] = this;
        }
        
        internal static void ChangeScene()
        {
            sceneObjects.Clear();
        }

        #region RPC Processing
        Dictionary<byte, Action<NetIncomingMessage>> RPCProcessors = new Dictionary<byte, Action<NetIncomingMessage>>();

        /// <summary>
        /// Subscribe to an rpc
        /// </summary>
        /// <param name="rpcID">id of the rpc</param>
        /// <param name="rpcProcessor">action to process the rpc with</param>
        /// <param name="overwriteExisting">overwrite the existing processor if one exists.</param>
        /// <returns>Whether or not the rpc was subscribed to. Will return false if an existing rpc was attempted to be subscribed to, and overwriteexisting was set to false</returns>
        public bool SubscribeToRPC(byte rpcID, Action<NetIncomingMessage> rpcProcessor, bool overwriteExisting = true)
        {
            if (rpcProcessor == null)
                throw new ArgumentNullException("rpcProcessor", "the processor delegate cannot be null");
            if (overwriteExisting)
            {
                RPCProcessors[rpcID] = rpcProcessor;
                return true;
            }
            else
            {
                Action<NetIncomingMessage> checkExist;
                if (RPCProcessors.TryGetValue(rpcID, out checkExist))
                {
                    return false;
                }
                else
                {
                    RPCProcessors.Add(rpcID, checkExist);
                    return true;
                }
            }
        }

        /// <summary>
        /// Unsubscribe from an rpc
        /// </summary>
        /// <param name="rpcID"></param>
        public void UnsubscribeFromRPC(byte rpcID)
        {
            RPCProcessors.Remove(rpcID);
        }

        internal static void CallRPC(int id, byte rpcID, NetIncomingMessage message)
        {
            NetworkedSceneObject sceneObject;
            if (sceneObjects.TryGetValue(id, out sceneObject))
            {
                Action<NetIncomingMessage> processor;
                if (sceneObject.RPCProcessors.TryGetValue(rpcID, out processor))
                {
                    if (processor != null)
                        processor(message);
                    else
                    {
                        Debug.LogWarning("RPC processor for {0} was null. Automatically cleaning up. Please be sure to clean up after yourself in the future.", rpcID);
                        sceneObject.RPCProcessors.Remove(rpcID);
                    }
                }
                else
                {
                    Debug.LogWarning("NetworkedSceneObject on {0}: unhandled RPC {1}", id, rpcID);
                }
            }
            
        }

        #endregion

        /// <summary>
        /// Send an rpc to the server
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="args"></param>
        public void RPC(byte rpcID, params INetSerializable[] args)
        {
            var size = 3;
            RPCUtils.AllocSize(ref size, args);

            var message = Net.Peer.CreateMessage(size);
            message.Write((ushort)NetworkID);
            message.Write(rpcID);
            RPCUtils.WriteParams(ref message, args);

            Net.Peer.SendMessage(message, NetDeliveryMethod.ReliableOrdered, Channels.OBJECT_RPC);
        }

        /// <summary>
        /// serialize this into a string
        /// </summary>
        /// <returns></returns>
        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.AppendLine("-NetworkedSceneObject-");
            sb.Append("id:").Append(NetworkID).AppendLine(";");

            return sb.ToString();
        }

        private static Dictionary<int, NetworkedSceneObject> sceneObjects = new Dictionary<int, NetworkedSceneObject>();
    }
}