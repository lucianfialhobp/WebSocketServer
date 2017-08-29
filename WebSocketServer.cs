﻿using CitizenFX.Core;
using CitizenFX.Core.Native;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using vtortola.WebSockets;

namespace WebSocketServer
{
	class WebSocketServer : BaseScript
	{
		private enum LogLevels { Debug, Info, Warn, Error };
		private LogLevels logLevel;

		private string authorization;
		private List<string> events;

		public WebSocketServer()
		{
			logLevel = Function.Call<string>(Hash.GET_CONVAR, "websocket_debug", "false") == "true" ? LogLevels.Debug : LogLevels.Info;

			authorization = Function.Call<string>(Hash.GET_CONVAR, "websocket_authorization", "");
			events = new List<string>();

			EventHandlers["WebSocketServer:addListener"] += new Action<dynamic>((dynamic eventName) =>
			{
				lock (events)
				{
					if (!events.Contains((string) eventName))
					{
						events.Add((string) eventName);
					}
				}
			});

			IPAddress listeningHost = IPAddress.Loopback;
			IPAddress.TryParse(Function.Call<string>(Hash.GET_CONVAR, "websocket_host", "127.0.0.1"), out listeningHost);
			int listeningPort = Function.Call<int>(Hash.GET_CONVAR_INT, "websocket_port", 80);
			var endpoint = new IPEndPoint(listeningHost, listeningPort);

			var server = new WebSocketEventListener (endpoint, new WebSocketListenerOptions () {
				SubProtocols = new String[]{ "text" },
				OnHttpNegotiation = (request, response) =>
				{
					if (authorization.Length > 0)
					{
						var authHeader = request.Headers["Authorization"];
						if (authHeader == null || authHeader != authorization)
						{
							response.Status = HttpStatusCode.Unauthorized;
							Log("Rejected Authorization header: " + authHeader, LogLevels.Debug);
						}
					}
				}
			});
			server.OnConnect += (ws)=> Log("Connection from " + ws.RemoteEndpoint.ToString(), LogLevels.Debug);
			server.OnDisconnect += (ws) => Log("Disconnection from " + ws.RemoteEndpoint.ToString(), LogLevels.Debug);
			server.OnError += (ws, ex) => Log("Error: " + ex.Message, LogLevels.Debug);
			server.OnMessage += (ws, msg) =>
			{
				Log("Received message: " + msg, LogLevels.Debug);

				lock (events)
				{
					foreach (var eventName in events)
					{
						TriggerEvent(eventName, msg);
					}
				}
			};

			try
			{
				server.Start();
				Tick += server.ListenAsync;

				Log("Started at " + endpoint.ToString());
			}
			catch (Exception e)
			{
				Log("Can't start server at " + endpoint.ToString() + ": " + e.Message);
			}
		}

		private void Log(string message, LogLevels level = LogLevels.Info)
		{
			if (logLevel > level)
			{
				return;
			}

			Console.WriteLine("[WebSocket Server] " + message);
		}
	}
}