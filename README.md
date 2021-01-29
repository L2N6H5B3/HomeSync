# HomeSync
Syncronise Windows Media Center playback and recording across multiple PCs on the network

## What is HomeSync
HomeSync is intended to be a "one-stop-shop" to streamline the experience of using Windows Media Center across multiple machines.

**Current Features Include:**
* Syncronised Resume Points (resume Recorded TV playback on any machine)<sup>*</sup>

<sup>*</sup>Requires shared Recorded TV folders to be added to the Media Center Recorded TV Library and Recorded TV Shows to be discovered prior to running

**Features in Development:**
* Recording Brokering (from one machine to a main recording machine on the network)
* XMLTV Guide Import & Distribution

**Future Ideas:**
* Automatically discover and import Recorded TV Shows and metadata
* Adopt a Mesh Architecture over the current Client-Server Architecture
* Implement Resume Point Conflict Resolution

## Configuration
HomeSync communicates over TCP port 65430 (this can be changed in the .config file), utilises a typical "client-server" architecture.  Each HomeSync use case requires a server machine (normally the machine running the main Windows Media Center installation, located in the lounge or home theatre), and at least one client machine (bedroom, den, etc).

The server machine *must* have a static IP address configured.  HomeSync does not support locating the server through NetBIOS or domain names at this time.

Before installing HomeSync, one must be aware that the Recorded TV Folder on your main server machine (and any other client machines that have Recorded TV available) must be shared and accessible by all machines on which you wish to install HomeSync.  Each folder must be added to each Windows Media Center Recorded TV Library and have the contents of the folder populated in the Recorded TV area.

Once the above is complete, installation of HomeSync is simple - download the latest HomeSync releases of both the Client and Server, extract the folders to a convenient location on the server and each client, and create a shortcut to run HomeSync every time the PC is started (usually the Startup folder in the local user account's Start Menu).  Then simply open the application, enter the IP address of the machine running HomeSync Server, and "**Save and Close**".

## Other Notes & Data Flow
HomeSync is designed to cope with one or multiple machines being unavailable for a short period as HomeSync will buffer resume point updates in memory and attempt to re-transmit these whenever the machines become available again.

Below are a few use cases with the example flow of data:
* A recording is partly watched and then stopped on the Server
  * The recording resume point will be distributed to all clients immediately or when they become available
    * If the client does not become available prior to the server being rebooted, the client will not receive the resume update as this was stored in the buffer in memory, and the client will retain its current restore point unless it is rebooted
* A recording is partly watched and then stopped on a client
  * The recording resume point will be sent to the server immediately or when it becomes available, and the server will distribute this update immediate to clients or when they become available
    * If the server does not become available prior to the client being rebooted, the server nor any other clients will not receive the resume update as this was stored in the buffer in memory, and the resume point will be lost


## Troubleshooting
Both HomeSync Server and HomeSync Client write logs which can be used for troubleshooting of any communication issues or program crashes.  Logs are written to the local user's application data folder: "**AppData\Local\HomeSync <Client / Server>\**".
