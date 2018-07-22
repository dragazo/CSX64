# CSX64
CSX64 is a 64-bit processor emulator implemented in C# (custom machine code - subset of Intel instruction set). It comes with a built-in, thorough assembly language loosely based around NASM.

CSX64 was designed to be an educational tool for learning low-level programming in a well-documented, safe, emulated environment that is **platform independent** *(insofar as C# is platform independent)*. Additionally, it was designed to be self-contained so that it could be added to other projects (e.g. a Unity game). If your machine is incapable of running C# applications, you can check out CSX64's [C++ implementation](https://github.com/dragazo/CSX64-cpp) (though it lacks the graphical client and is still highly experimental).

All emulation features are held within a single C# class split up over several files in [src/CSX64](src/CSX64). All assembly features are held within [Assembly.cs](src/CSX64/Assembly.cs). The emulator and assembly can function independently, but they both require resources from [Utility.cs](src/CSX64/Utility.cs) and [Types.cs](src/CSX64/Types.cs).

Documentation on the instruction set and assembly language *(as well as other information for users who are trying to learn processor details/assembly language)* are available in [CSX64 Specification](CSX64%20Specification.pdf).

## Getting you own Build
To get your own build, start out with a repo clone (via `git clone --recurse-submodules https://github.com/dragazo/CSX64`). This will create a new folder named CSX64 that looks something like this:

![clone](img/cloning/after_clone.png)

CSX64 is built in [Visual Studio Community 2017](https://www.visualstudio.com/downloads/) *(though most versions should still work)*. Open the `.sln` file (selected in the above image) in VisualStudio. This will bring you to the following window:

![pick release](img/cloning/vs_pick_release.png)

Make sure `Release` mode is selected (see above), then select `Build > Build Solution`:

![build](img/cloning/vs_build.png)

You should now have an executable at `release/csx.exe`. This is a console application, which I'll demonstrate in PowerShell:

![run](img/cloning/run_exe.png)

Now you need to assemble CSX64's stdlib (located in the `asm` directory) and place the resulting object files in the same directory as the executable *(and in the same hierarchy)*. To make this easier, a bash script named [update.sh](update.sh) is included in CSX64's root directory. Open up your bash terminal *(e.g. git bash)* and enter the following command:

```
./update.sh release
```

**Boom, you're done.** CSX64 doesn't need to be installed: all you need is the executable and the object files you just created.

If you plan on using CSX64 from a different directory than the executable, you may want to move it to a safe location (e.g. Documents or Program Files on windows systems) and edit your system environment variables to access it remotely from the console. To do this on windows (10), open up settings and search for `environment`. Select edit system environment variables. This should open the `System Properties` diaglog. Select `Advanced > Environment Variables`. Be very careful in here, but what you're looking for is the `PATH` variable (either the system variable or the account variable). Select this one and hit `Edit`. In the dialog that shows, click `New` and paste in the absolute path to the directory of your executable (not to the executable itself). Hit ok on everything and you should be good to go. From now on, you can use `csx` in the terminal as if it were an installed program. I make a lot of programs that I use regularly in the terminal, so here's a piece of advice: make one folder in a safe location and add it to the `PATH` variable - then when you want to add/remove a personal program you can just add/remove it from that folder rather than having to mess with the environment variables every single time.

For more information on CSX64, see the [Specification](CSX64%20Specification.pdf).
