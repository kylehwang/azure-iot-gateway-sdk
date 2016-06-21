﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.IoT.Gateway
{
    /// <summary> Object that represents a message on the message bus. </summary>
    public class Message
    {
        public byte[] Content { get; }

        public Dictionary<string, string> Properties { get; }

        private byte[] readNullTerminatedByte(MemoryStream bis)
        {
            ArrayList byteArray = new ArrayList();

            byte b = (byte)bis.ReadByte();

            if (b == 255)
            {
                throw new ArgumentException("Reached and of stream and '\0' was not found.");
            }

            while (b != '\0')
            {
                byteArray.Add(b);
                b = (byte)bis.ReadByte();

                if (b == 255)
                {
                    throw new ArgumentException("Reached and of stream and '\0' was not found.");
                }
            }

            byte[] result = new byte[byteArray.Count];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = (byte)byteArray[index];
            }

            return result;
        }

        private int readIntFromMemoryStream(MemoryStream input)
        {
            byte[] byteArray = new byte[4];

            int numberofBytes = input.Read(byteArray, 0, 4);

            if(numberofBytes < 4)
            {
                throw new ArgumentException("Input doesn't have 4 bytes.");
            }

            if(BitConverter.IsLittleEndian)
            {
                Array.Reverse(byteArray); //Have to reverse because BitConverter expects a MSB 
            }            
            return BitConverter.ToInt32(byteArray, 0);
        }

        /// <summary>
        ///     Constructor for Message. This receives a byte array (defined at spec [message_requirements.md](../C:\repos\azure-iot-gateway-sdk\core\devdoc\message_requirements.md)).
        /// </summary>
        /// <param name="msgInByteArray">ByteArray with the Content and Properties of a message.</param>
        public Message(byte[] msgAsByteArray)
        {
            //Requirement: 
            //- 2 (0xA1 0x60) = fixed header
            //- 4(0x00 0x00 0x00 0x0E) = arrray size[14 bytes in total]
            //- 4(0x00 0x00 0x00 0x00) = 0 properties that follow
            //- 4(0x00 0x00 0x00 0x00) = 0 bytes of message content

            if (msgAsByteArray == null)
            {
                /* Codes_SRS_DOTNET_MESSAGE_04_008: [ If any parameter is null, constructor shall throw a ArgumentNullException ] */
                throw new ArgumentNullException("msgAsByteArray cannot be null");                    
            }
            /* Codes_SRS_DOTNET_MESSAGE_04_002: [ Message class shall have a constructor that receives a byte array with it's content format as described in message_requirements.md and it's Content and Properties are extracted and saved. ] */
            else if (msgAsByteArray.Length >= 14)
            {
                MemoryStream stream = new MemoryStream(msgAsByteArray);
                this.Properties = new Dictionary<string, string>();

                byte header1 = (byte)stream.ReadByte();
                byte header2 = (byte)stream.ReadByte();

                if (header1 == (byte)0xA1 && header2 == (byte)0x60)
                {
                    int arraySizeInInt;
                    try
                    {
                        arraySizeInInt = readIntFromMemoryStream(stream);
                    }
                    catch(ArgumentException e)
                    {
                        /* Codes_SRS_DOTNET_MESSAGE_04_006: [ If byte array received as a parameter to the Message(byte[] msgInByteArray) constructor is not in a valid format, it shall throw an ArgumentException ] */
                        throw new ArgumentException("Could not read array size information.", e);
                    }
                    

                    if(msgAsByteArray.Length != arraySizeInInt)
                    {
                        /* Codes_SRS_DOTNET_MESSAGE_04_006: [ If byte array received as a parameter to the Message(byte[] msgInByteArray) constructor is not in a valid format, it shall throw an ArgumentException ] */
                        throw new ArgumentException("Array Size information doesn't match with array size.");
                    }

                    int propCount;

                    try
                    {
                        propCount = readIntFromMemoryStream(stream);
                    }
                    catch (ArgumentException e)
                    {
                        /* Codes_SRS_DOTNET_MESSAGE_04_006: [ If byte array received as a parameter to the Message(byte[] msgInByteArray) constructor is not in a valid format, it shall throw an ArgumentException ] */
                        throw new ArgumentException("Could not read property count.", e);
                    }

                    if (propCount >= int.MaxValue)
                    {
                        /* Codes_SRS_DOTNET_MESSAGE_04_006: [ If byte array received as a parameter to the Message(byte[] msgInByteArray) constructor is not in a valid format, it shall throw an ArgumentException ] */
                        throw new ArgumentException("Number of properties can't be more than MAXINT.");
                    }

                    if(propCount < 0)
                    {
                        /* Codes_SRS_DOTNET_MESSAGE_04_006: [ If byte array received as a parameter to the Message(byte[] msgInByteArray) constructor is not in a valid format, it shall throw an ArgumentException ] */
                        throw new ArgumentException("Number of properties can't be negative."); 
                    }

                    if (propCount > 0)
                    {
                        //Here is where we are going to read the properties.
                        for (int count = 0; count < propCount; count++)
                        {
                            try
                            {
                                byte[] key = readNullTerminatedByte(stream);
                                byte[] value = readNullTerminatedByte(stream);
                                this.Properties.Add(System.Text.Encoding.UTF8.GetString(key, 0, key.Length), System.Text.Encoding.UTF8.GetString(value, 0, value.Length));
                            }
                            catch(ArgumentException e)
                            {
                                /* Codes_SRS_DOTNET_MESSAGE_04_006: [ If byte array received as a parameter to the Message(byte[] msgInByteArray) constructor is not in a valid format, it shall throw an ArgumentException ] */
                                throw new ArgumentException("Could not parse Properties(key or value)", e);
                            }


                        }
                    }

                    long contentLengthPosition = stream.Position;
                    int contentLength;

                    try
                    {
                        contentLength = readIntFromMemoryStream(stream);
                    }
                    catch (ArgumentException e)
                    {
                        /* Codes_SRS_DOTNET_MESSAGE_04_006: [ If byte array received as a parameter to the Message(byte[] msgInByteArray) constructor is not in a valid format, it shall throw an ArgumentException ] */
                        throw new ArgumentException("Could not read contentLength.", e);
                    }
                    

                    // Verify if the number of content matches with the real number of content. 
                    // 4 is the number of bytes that describes the contentLengthPosition information.
                    if (arraySizeInInt - contentLengthPosition - 4 != contentLength)
                    {
                        /* Codes_SRS_DOTNET_MESSAGE_04_006: [ If byte array received as a parameter to the Message(byte[] msgInByteArray) constructor is not in a valid format, it shall throw an ArgumentException ] */
                        throw new ArgumentException("Size of byte array doesn't match with current content.");
                    }

                    byte[] content = new byte[contentLength];
                    stream.Read(content, 0, contentLength);

                    this.Content = content;
                }
                else
                {
                    /* Codes_SRS_DOTNET_MESSAGE_04_006: [ If byte array received as a parameter to the Message(byte[] msgInByteArray) constructor is not in a valid format, it shall throw an ArgumentException ] */
                    throw new ArgumentException("Invalid Header bytes.");
                }
            }
            else
            {
                /* Codes_SRS_DOTNET_MESSAGE_04_006: [ If byte array received as a parameter to the Message(byte[] msgInByteArray) constructor is not in a valid format, it shall throw an ArgumentException ] */
                throw new ArgumentException("Invalid byte array size.");
            }
        }

        /// <summary>
        ///     Constructor for Message. This constructor receives a byte[] as it's content and Properties.
        /// </summary>
        /// <param name="contentAsByteArray">Content of the Message</param>
        /// <param name="properties">Set of Properties that will be added to a message.</param>
        public Message(byte[] contentAsByteArray, Dictionary<string, string> properties)
        {
            if (contentAsByteArray == null)
            {
                /* Codes_SRS_DOTNET_MESSAGE_04_008: [ If any parameter is null, constructor shall throw a ArgumentNullException ] */
                throw new ArgumentNullException("contentAsByteArray cannot be null");
            }
            else if(properties == null)
            {
                /* Codes_SRS_DOTNET_MESSAGE_04_008: [ If any parameter is null, constructor shall throw a ArgumentNullException ] */
                throw new ArgumentNullException("properties cannot be null");
            }
            else
            {
                /* Codes_SRS_DOTNET_MESSAGE_04_004: [ Message class shall have a constructor that receives a content as byte[] and properties, storing them. ] */
                this.Content = contentAsByteArray;
                this.Properties = properties;
            }
        }

        /// <summary>
        ///     Constructor for Message. This constructor receives a string as it's content and Properties.
        /// </summary>
        /// <param name="content">String with the ByteArray with the Content and Properties of a message.</param>
        /// <param name="properties">Set of Properties that will be added to a message.</param>
        public Message(string content, Dictionary<string, string> properties)
        {
            
            if (content == null)
            {
                /* Codes_SRS_DOTNET_MESSAGE_04_008: [ If any parameter is null, constructor shall throw a ArgumentNullException ] */
                throw new ArgumentNullException("content cannot be null");
            }
            else if (properties == null)
            {
                /* Codes_SRS_DOTNET_MESSAGE_04_008: [ If any parameter is null, constructor shall throw a ArgumentNullException ] */
                throw new ArgumentNullException("properties cannot be null");
            }
            else
            {
                /* Codes_SRS_DOTNET_MESSAGE_04_003: [ Message class shall have a constructor that receives a content as string and properties and store it. This string shall be converted to byte array based on System.Text.Encoding.UTF8.GetBytes(). ] */
                this.Content = System.Text.Encoding.UTF8.GetBytes(content);
                this.Properties = properties;
            }
            
        }

        public Message(Message message)
        {
            if(message == null)
            {
                /* Codes_SRS_DOTNET_MESSAGE_04_008: [ If any parameter is null, constructor shall throw a ArgumentNullException ] */
                throw new ArgumentNullException("message cannot be null");
            }
            throw new NotImplementedException();
        }


        private int getPropertiesByteAmount()
        {
            int sizeOfPropertiesInBytes = 0;
            foreach (KeyValuePair<string, string> propertiItem in this.Properties)
            {
                sizeOfPropertiesInBytes += propertiItem.Key.Length + 1;
                sizeOfPropertiesInBytes += propertiItem.Value.Length + 1;
            }

            return sizeOfPropertiesInBytes;
        }

        private int fillByteArrayWithPropertyInBytes(byte[] dst)
        {
            //The content needs to be filled from byte 11th. 
            int currentIndex = 10;

            foreach (KeyValuePair<string, string> propertiItem in this.Properties)
            {
                for (int currentChar = 0; currentChar < propertiItem.Key.Length; currentChar++)
                {
                    dst[currentIndex++] = (byte)propertiItem.Key[currentChar];
                }

                dst[currentIndex++] = 0;

                for (int currentChar = 0; currentChar < propertiItem.Value.Length; currentChar++)
                {
                    dst[currentIndex++] = (byte)propertiItem.Value[currentChar];
                }

                dst[currentIndex++] = 0;
            }

            return currentIndex;
        }

        /// <summary>
        ///    Converts the message into a byte array (defined at spec [message_requirements.md](../C:\repos\azure-iot-gateway-sdk\core\devdoc\message_requirements.md)).
        /// </summary>
        virtual public byte[] ToByteArray()
        {
            //Now this is the point where I will serialize a message. 
            //Requirement: 
            //- 2 (0xA1 0x60) = fixed header
            // -4(0x00 0x00 0x00 0x0E) = arrray size[14 bytes in total]
            //- 4(0x00 0x00 0x00 0x00) = 0 properties that follow
            //- 4(0x00 0x00 0x00 0x00) = 0 bytes of message content

            /* Codes_SRS_DOTNET_MESSAGE_04_005: [ Message Class shall have a ToByteArray method which will convert it's byte array Content and it's Properties to a byte[] which format is described at message_requirements.md ] */
            //1-Calculate the size of the array;
            int sizeOfArray = 2 + 4 + 4 + getPropertiesByteAmount() + 4 + this.Content.Length;

            //2-Create the byte array;
            byte[] returnByteArray = new Byte[sizeOfArray];

            //3-Fill the first 2 bytes with 0xA1 and 0x60
            returnByteArray[0] = 0xA1;
            returnByteArray[1] = 0x60;

            //4-Fill the 4 bytes with the array size;
            byte[] sizeOfArrayByteArray = BitConverter.GetBytes(sizeOfArray);
            Array.Reverse(sizeOfArrayByteArray); //Have to reverse because this is not MSB and needs to be.
            returnByteArray[2] = sizeOfArrayByteArray[0];
            returnByteArray[3] = sizeOfArrayByteArray[1];
            returnByteArray[4] = sizeOfArrayByteArray[2];
            returnByteArray[5] = sizeOfArrayByteArray[3];

            //5-Fill the 4 bytes with the amount of properties;
            byte[] numberOfPropertiesInByteArray = BitConverter.GetBytes(this.Properties.Count);
            Array.Reverse(numberOfPropertiesInByteArray); //Have to reverse because this is not MSB and needs to be. 
            returnByteArray[6] = numberOfPropertiesInByteArray[0];
            returnByteArray[7] = numberOfPropertiesInByteArray[1];
            returnByteArray[8] = numberOfPropertiesInByteArray[2];
            returnByteArray[9] = numberOfPropertiesInByteArray[3];

            //6-Fill the bytes with content from key/value of properties (null terminated string separated);
            int msgContentShallStartFromHere = fillByteArrayWithPropertyInBytes(returnByteArray);

            //7-Fill the amount of bytes on the content in 4 bytes after the properties; 
            byte[] contentSizeInByteArray = BitConverter.GetBytes(this.Content.Length);
            Array.Reverse(contentSizeInByteArray); //Have to reverse because this is not MSB and needs to be. 
            returnByteArray[msgContentShallStartFromHere++] = contentSizeInByteArray[0];
            returnByteArray[msgContentShallStartFromHere++] = contentSizeInByteArray[1];
            returnByteArray[msgContentShallStartFromHere++] = contentSizeInByteArray[2];
            returnByteArray[msgContentShallStartFromHere++] = contentSizeInByteArray[3];

            //8-Fill up the bytes with the message content. 

            foreach (byte contentElement in this.Content)
            {
                returnByteArray[msgContentShallStartFromHere++] = contentElement;
            }


            return returnByteArray;
        }
    }
}