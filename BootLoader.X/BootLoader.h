/* 
 * File:   BootLoader.h
 * Author: Chris Lomont
 *
 * Created on May 18, 2015, 11:35 AM
 */

#ifndef BOOTLOADER_H
#define	BOOTLOADER_H

#ifdef	__cplusplus
extern "C" {
#endif

#include <stdint.h>  // basic types like uint16_t, etc.
#include <stdbool.h> // type bool

// See the Bootloader.c file for documentation

// This is the bootloader entry point, which can be called from an application,
// but is best called before anything else from the hardware reset vector.
// It returns a value indicating what happened. This value is also stored in
// the variable bootResult
uint8_t BootloaderEntry();

// one return variable at bottom of ram. See the C file for values.
extern __attribute__((section(".hcram"))) uint8_t bootResult;

// check assumptions needed for proper bootloader functioning.
// return true on success, else false
bool BootTestAssumptions();

// Get a text string for the bootloader version
const char * BootloaderVersion();

#ifdef	__cplusplus
}
#endif

#endif	/* BOOTLOADER_H */

