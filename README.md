# SSD - Specs for Storage Devices
### SSD is a simple and lightweight tool to obtain some information about the storage devices found inside of your computer

# How it works
SSD parses the output of [smartctl.exe](https://www.smartmontools.org/wiki/Download), gathering the required data to supply the infromation of the drives to the program.
- First it determines the storage devices in your computer using the `smartctl.exe --scan` command
- Then it runs the command `smartctl.exe -x /dev/{num_of_storage_device}` for each drive found in your system
- After that it parses the output of the commands using regular expressions
- If the S.M.A.R.T. data does not provide the condition of the device (which it usually never does for HDD-s) it uses an algorithm based on some factors to calculate it

# Compatibility
SSD is made in .NET 4.0 so it should theoradically run on systems as low as Windows XP SP3 but its functionality is yet to be tested on older systems

# How to build
- Download the project as a .ZIP from this GitHub page
- Open the .sln is Visual Studio
- Click 'Build' --> 'Build Solution'
- When it is done building make sure to place [smartctl.exe](https://sourceforge.net/projects/smartmontools/files/smartmontools/7.4/smartmontools-7.4-1.win32-x86-pre-vista-setup.exe/download) in the same directory as `SSD.exe` (It is already included in the release version, you only need to do it if you build it yourself)

# Features
SSD lists the following informations about your drives:
- Model
- Health
- Temperature
- Power-On Hours
- Start / Stop Count (In case of HDD)
- HWID
- Health
- GigaBytes Written (In case of SSD)
- Bas Sectors (In case of HDD)

# Why SSD?
While this program might not be as advanced as other storage device info tools like `CrystalDisk Info` or `HD Sentinel` it is still a good choice if you desperately need something to check the status of your drives

# Credits
Credits to [smartmontools](https://www.smartmontools.org/) for smartctl.exe
