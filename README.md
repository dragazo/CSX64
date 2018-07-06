# CSX64
CSX64 is a 64-bit processor emulator implemented in C# (custom machine code - subset of Intel instruction set). It comes with a built-in, thorough assembly language loosely based around NASM.

CSX64 was designed to be an educational tool for learning low-level programming in a well-documented, safe, emulated environment that is **platform independent** *(insofar as C# is platform independent)*. Additionally, it was designed to be self-contained so that it could be added to other projects (e.g. a Unity game).

All emulation features are held within a single C# class split up over several files in [src/CSX64](src/CSX64). All assembly features are held within [Assembly.cs](src/CSX64/Assembly.cs). The emulator and assembly can function independently, but they both require resources from [Utility.cs](src/CSX64/Utility.cs) and [Types.cs](src/CSX64/Types.cs).

Documentation on the instruction set and assembly language *(as well as other information for users who are trying to learn processor details/assembly language)* are available in [CSX64 Specification](CSX64%20Specification.pdf).

## Getting you own Build
To get your own build, start out with a repo clone (either via github's gui or via `git clone https://github.com/dragazo/CSX64`). This will create a new folder named CSX64 that looks something like this:

![clone](img/cloning/after_clone.png)

CSX64 is built in [Visual Studio Community 2017](https://www.visualstudio.com/downloads/) *(though most versions should still work)*. Open the `.sln` file (selected in the above image) in VisualStudio. This will bring you to the following window:

![pick release](img/cloning/vs_pick_release.png)

Make sure `Release` mode is selected (see above), then select `Build > Build Solution`:

![build](img/cloning/vs_build.png)

You should now have an executable at `bin/Release/csx.exe`. This is a console application, which I'll demonstrate in PowerShell:

![run](img/cloning/run_exe.png)

Now you need to assemble CSX64's standard library and place the resulting object files in a folder named `stdlib` in the same directory as the executable. To do this, open up your (linux-like) terminal *(e.g. git bash if on windows)* **in the same directory as your new executable** and run one of the following sets of commands:

```bash
mkdir stdlib
find ../../stdlib -name "*.asm" | xargs -n 1 basename -s ".asm" | xargs -i ./csx.exe ../../stdlib/{}.asm -ao stdlib/{}.o
```

**OR**

```bash
mkdir stdlib
cp ../../stdlib/*.asm stdlib/.
./csx.exe stdlib/*.asm -a
rm stdlib/*.asm
```

If neither of those worked (e.g. not using a linux-like terminal), you'll have to assemble them manually, for which the relevant command is `./csx.exe <input> -ao <output>` for each file. Alternatively, you could skip this step entirely and just leave the stdlib folder empty (though this would also mean not having access to those functions implicitly).

**Boom, you're done.** CSX64 doesn't need to be installed: all you need is the executable and the stdlib folder you just created.

If you plan on using CSX64 from a different directory than the executable, you may want to move it to a safe location (e.g. Documents or Program Files on windows systems) and edit your system environment variables to access it remotely from the console. To do this on windows (10), open up settings and search for `environment`. Select edit system environment variables. This should open the `System Properties` diaglog. Select `Advanced > Environment Variables`. Be very careful in here, but what you're looking for is the `PATH` variable (either the system variable or the account variable). Select this one and hit `Edit`. In the dialog that shows, click `New` and paste in the absolute path to the directory of your executable (not to the executable itself). Hit ok on everything and you should be good to go. From now on, you can use `csx` in the terminal as if it were an installed program.

For more information on CSX64, see the [Specification](CSX64%20Specification.pdf).
