# Hypnocube PIC32 Bootloader

[**Who we are**](#who-we-are) **|** 
[**Using the program**](#using-the-program) **|** 
[**Miscellaneous**](#miscellaneous) **|** 
[**License**](#copyright-and-license)

*******************************************************************************
For gadgets we build at [Hypnocube](http://hypnocube.com/) we wanted a bootloader for PIC32 based microcontrollers, and although there were already many existing ones, none had the combination of features or license we desired, so we wrote a new one. Features include

* MIT License - you can do what you want with it.
* Supports encrypted images through use of [ChaCha20](http://cr.yp.to/chacha.html) encrypted streams.
* Decent protection against bricking the PIC in the field.
* Should run on all PIC32 devices (tested on those we use).
* Flexible and well documented (read the code, in particular Bootloader.c)
* UART only connection (saves space by not supporting lots of communication protocols) 

It was tested on our HypnoLSD module, which is a LED strand controller, and gives us a PIC32 platform with programming pins and other features for quick prototyping for us. 

## Who we are

We are the makers of the [Hypnocube](http://hypnocube.com/), a 4x4x4 LED light art gadget, as well as some other similar devices. We wanted to release some code for people to play with and make it easier to interact with our upcoming devices. Our most famous gadget is the 4x4x4 Hypnocube:

<img src="http://hypnocube.com/wp-content/uploads/2011/10/IMG_0684.jpg" alt="The 4x4x4 Hypnocube" width="350">

## Using the program
The bootloader currently has two main components: the **[PIC bootloader code](BootLoader.X)** and the **[C# console flashing program](PICFlasher)**.

How to use the bootloader from the PIC32 side is described in the code itself ([Bootloader.c](BootLoader.X/BootLoader.c)).

How to use the console flasher is described in the program when you run it.

The basic idea is you build the bootloader with your program, and after your PIC is done, if you need to update the program, you compile a new one, run the flash utility with the name of your hex file, a file for your optional encryption key, and an optional filename to make an encrypted image to distribute. Then plug in the PIC through a serial port, and the flasher will connect and let you flash the image. Simple :)

## Miscellaneous

### Bugs and Feature Requests
Send them to us, but we're busy and may not get to them, but we will add them to a list and each time we take a pass on this code we will do anything that we find suitable.

### Release History
* 0.5 - May 2015 - Initial release to the public

### TODO
* Make more robust :) 
 

### Contributing

If you want to make changes or extend this code, feel free to do so, and if you tell us about them, we will integrate any we feel suitable back into this official location. Follow our website for upcoming and new devices.

### Creators

This code was written by Chris Lomont. 

**Chris Lomont**

- Email: [chris@hypnocube.com](mailto:chris@hypnocube.com)
- [Personal website (outdated)](http://www.lomont.org)

## Copyright and License

Although we cannot stop you from forking this, please don't. We've had problems from people forking things we release then others wanting us to deal with the fork. Send us bug fixes or comments and make this the main location for this code and derivatives. Of course you're welcome to use the code in your public project as you desire.  

This code released under the [MIT license](LICENSE).

THE END
